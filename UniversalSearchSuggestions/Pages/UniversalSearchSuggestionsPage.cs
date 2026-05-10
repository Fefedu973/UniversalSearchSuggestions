using System.Text;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using UniversalSearchSuggestions.Commands;
using UniversalSearchSuggestions.Core.Browsers;
using UniversalSearchSuggestions.Core.Resources;
using UniversalSearchSuggestions.Core.Search;
using UniversalSearchSuggestions.Core.Search.Providers;
using UniversalSearchSuggestions.Core.Utilities;
using UniversalSearchSuggestions.Icons;
using UniversalSearchSuggestions.RichDetails;
using UniversalSearchSuggestions.Settings;
using UniversalSearchSuggestions.Storage;

namespace UniversalSearchSuggestions.Pages;

internal sealed partial class UniversalSearchSuggestionsPage : DynamicListPage, IDisposable
{
    public const string PageId = "com.fefedu973.universalsearchsuggestions.page";

    private static readonly KeyChord CopyShortcut = KeyChordHelpers.FromModifiers(
        ctrl: true, alt: false, shift: false, win: false, vkey: 0x43, scanCode: 0); // 'C'

    private static readonly KeyChord IncognitoShortcut = KeyChordHelpers.FromModifiers(
        ctrl: true, alt: false, shift: true, win: false, vkey: 0x4E, scanCode: 0); // Ctrl+Shift+N

    private static readonly KeyChord PrivateShortcut = KeyChordHelpers.FromModifiers(
        ctrl: true, alt: false, shift: true, win: false, vkey: 0x50, scanCode: 0); // Ctrl+Shift+P

    private readonly SearchSettingsManager _settingsManager;
    private readonly HttpClient _httpClient;
    private readonly SuggestionFetchService _suggestionFetchService;
    private readonly FaviconCacheService _faviconCacheService;
    private readonly RichDetailsService _richDetailsService;
    private readonly SuggestionFilters _filters;
    private readonly StatusMessage _aiStatusMessage;
    private readonly string _imageCacheDirectory;
    private readonly object _itemsLock = new();
    private readonly object _statusLock = new();
    private readonly HashSet<string> _detailsTransitionRefreshed = new(StringComparer.Ordinal);

    private CancellationTokenSource _refreshCts = new();
    private long _requestVersion;
    private IListItem[] _items = [];
    private IReadOnlyList<SearchSuggestion> _displayedSuggestions = [];
    private SearchPreferences? _displayedPreferences;
    private IExtensionHost? _extensionHost;
    private bool _aiStatusVisible;

    public UniversalSearchSuggestionsPage(SearchSettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
        _httpClient = SearchHttpClientFactory.Create();
        _suggestionFetchService = new SuggestionFetchService(
            DefaultSuggestionProviders.Create(_httpClient),
            _httpClient);
        _imageCacheDirectory = ResolveImageCacheRoot();
        _faviconCacheService = new FaviconCacheService(
            _httpClient,
            Path.Combine(_imageCacheDirectory, "Favicons"));
        _faviconCacheService.FaviconsChanged += OnFaviconsChanged;
        _richDetailsService = new RichDetailsService(_httpClient, Path.Combine(_imageCacheDirectory, "RichDetails"));
        _richDetailsService.DetailsChanged += OnRichDetailsChanged;
        _richDetailsService.AiStreamingStarted += OnAiStreamingStarted;
        _richDetailsService.AiStreamingFinished += OnAiStreamingFinished;
        _filters = new SuggestionFilters();
        _filters.PropChanged += OnFilterPropertyChanged;
        _aiStatusMessage = new StatusMessage
        {
            Message = Strings.StatusAiStreaming,
            State = MessageState.Info,
            Progress = new ProgressState { IsIndeterminate = true },
        };

        Id = PageId;
        Icon = AppIcons.ExtensionLogo;
        Title = Strings.DockTitle;
        Name = Strings.DockTitle;
        PlaceholderText = Strings.PagePlaceholder;
        ShowDetails = true;
        EmptyContent = BuildEmptyContent(Strings.PageStartTyping, Strings.EmptyStateStartTypingSubtitle);

        var preferences = _settingsManager.Snapshot();
        Filters = preferences.ShowFilters ? _filters : null;
        if (preferences.EmptySearchSuggestionsMode != EmptySearchSuggestionsMode.None)
        {
            var version = Interlocked.Increment(ref _requestVersion);
            IsLoading = true;
            _ = RefreshEmptySearchAsync(version, preferences, _refreshCts.Token);
        }
    }

    public void SetExtensionHost(IExtensionHost? host)
    {
        _extensionHost = host;
    }

    private static string ResolveImageCacheRoot()
    {
        // The PowerToys host (Microsoft.CmdPal.UI) loads file:// images by opening the path
        // directly via FileStream. For packaged extensions, AppData/Local is redirected to
        // <package>\LocalCache\Local which the host might not always be allowed to read across
        // packages. ApplicationData.Current.LocalFolder is the canonical UWP per-package
        // local folder and is the path the official Microsoft samples write to. Falling back
        // to LocalApplicationData keeps the code working when WindowsAppSDK package context
        // is not yet initialized (e.g. unit tests).
        try
        {
            var localFolder = Windows.Storage.ApplicationData.Current?.LocalFolder?.Path;
            if (!string.IsNullOrWhiteSpace(localFolder))
            {
                return Path.Combine(localFolder!, "ImageCache");
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UniversalSearchSuggestions",
            "ImageCache");
    }

    private void ApplyFiltersVisibility(SearchPreferences preferences)
    {
        var desired = preferences.ShowFilters ? _filters : null;
        if (!ReferenceEquals(Filters, desired))
        {
            Filters = desired;
        }
    }

    public override IListItem[] GetItems()
    {
        lock (_itemsLock)
        {
            return _items;
        }
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        var version = Interlocked.Increment(ref _requestVersion);
        _refreshCts.Cancel();
        _refreshCts.Dispose();
        _refreshCts = new CancellationTokenSource();
        _richDetailsService.CancelPendingLoads();
        HideAiStatus();
        lock (_detailsTransitionRefreshed)
        {
            _detailsTransitionRefreshed.Clear();
        }

        var query = newSearch.Trim();
        var preferences = _settingsManager.Snapshot();
        ShowDetails = preferences.ShowDetails;
        ApplyFiltersVisibility(preferences);

        if (query.Length == 0)
        {
            EmptyContent = BuildEmptyContent(Strings.PageStartTyping, Strings.EmptyStateStartTypingSubtitle);

            if (preferences.EmptySearchSuggestionsMode == EmptySearchSuggestionsMode.None)
            {
                IsLoading = false;
                SetItems([], clearSuggestions: true);
                return;
            }

            SetItems([], clearSuggestions: true);
            IsLoading = true;
            _ = RefreshEmptySearchAsync(version, preferences, _refreshCts.Token);
            return;
        }

        EmptyContent = BuildEmptyContent(Strings.PageNoSuggestions(query), Strings.EmptyStateNoResultsSubtitle);

        SetSuggestions(SuggestionFetchService.BuildImmediateSuggestions(query, preferences), preferences);

        IsLoading = true;
        _ = RefreshAsync(query, version, _refreshCts.Token);
    }

    private async Task RefreshEmptySearchAsync(
        long version,
        SearchPreferences preferences,
        CancellationToken cancellationToken)
    {
        try
        {
            var suggestions = await _suggestionFetchService
                .GetEmptySearchSuggestionsAsync(preferences, RecentSearchStore.Load(), cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            if (version != Interlocked.Read(ref _requestVersion))
            {
                return;
            }

            SetSuggestions(suggestions, preferences);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (version == Interlocked.Read(ref _requestVersion))
            {
                IsLoading = false;
            }
        }
    }

    private async Task RefreshAsync(string query, long version, CancellationToken cancellationToken)
    {
        try
        {
            var preferences = _settingsManager.Snapshot();
            ShowDetails = preferences.ShowDetails;

            if (preferences.DebounceMilliseconds > 0)
            {
                await Task.Delay(preferences.DebounceMilliseconds, cancellationToken).ConfigureAwait(false);
            }

            var recentQueries = preferences.IncludeRecentInSearchResults
                ? RecentSearchStore.Load()
                : (IReadOnlyList<string>)Array.Empty<string>();

            var suggestions = await _suggestionFetchService
                .SearchAsync(query, preferences, recentQueries, cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            if (version != Interlocked.Read(ref _requestVersion))
            {
                return;
            }

            SetSuggestions(suggestions, preferences);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (version == Interlocked.Read(ref _requestVersion))
            {
                IsLoading = false;
            }
        }
    }

    private ListItem BuildListItem(SearchSuggestion suggestion, SearchPreferences preferences, bool applySection)
    {
        var image = ImageReferenceResolver.Resolve(
            suggestion.ImageUrl,
            _imageCacheDirectory,
            preferences.DecodeDataImages) ?? BuildFaviconImage(suggestion, preferences);

        return new ListItem(new OpenSearchTargetCommand(suggestion, preferences))
        {
            Title = suggestion.Title,
            Subtitle = BuildSubtitle(suggestion),
            Icon = BuildIcon(suggestion, image, preferences),
            Details = preferences.ShowDetails ? BuildDetails(suggestion, image, preferences) : null,
            MoreCommands = BuildMoreCommands(suggestion, preferences, _richDetailsService),
            Tags = preferences.ShowResultTags ? SuggestionTagBuilder.BuildTags(suggestion) : [],
            Section = applySection ? ResolveSection(suggestion) : string.Empty,
            TextToSuggest = preferences.EnableSearchBoxAutocomplete ? suggestion.TextToSuggest ?? suggestion.Query : string.Empty,
        };
    }

    private static IContextItem[] BuildMoreCommands(SearchSuggestion suggestion, SearchPreferences preferences, RichDetailsService richDetailsService)
    {
        var commands = new List<IContextItem>();
        var browser = ResolveBrowserForExtraCommands();

        if (browser is not null)
        {
            var profiles = BrowserProfileDetector.Detect(browser);
            var profileIndex = 0;
            foreach (var profile in profiles)
            {
                if (profile.IsDefault)
                {
                    continue;
                }

                profileIndex++;
                var openWithProfile = new OpenSearchTargetCommand(
                    suggestion,
                    preferences,
                    browser,
                    profile,
                    privateMode: false,
                    nameOverride: Strings.CommandOpenInProfile(profile.DisplayName));
                var profileItem = new CommandContextItem(openWithProfile)
                {
                    Title = Strings.CommandOpenInProfile(profile.DisplayName),
                    Icon = AppIcons.Profile,
                };

                var profileShortcut = BuildProfileShortcut(profileIndex);
                if (profileShortcut.HasValue)
                {
                    profileItem.RequestedShortcut = profileShortcut.Value;
                }

                commands.Add(profileItem);
            }

            if (SupportsPrivateBrowsing(browser.Kind))
            {
                var privateCommand = new OpenSearchTargetCommand(
                    suggestion,
                    preferences,
                    browser,
                    profile: null,
                    privateMode: true,
                    nameOverride: PrivateModeLabel(browser.Kind));
                commands.Add(new CommandContextItem(privateCommand)
                {
                    Title = PrivateModeLabel(browser.Kind),
                    Icon = AppIcons.Incognito,
                    RequestedShortcut = browser.Kind == BrowserKind.Edge ? PrivateShortcut : IncognitoShortcut,
                });
            }
        }

        if (CanShowDeepDive(suggestion, preferences))
        {
            var deepDive = new AiDeepDivePage(richDetailsService, suggestion.Query, preferences);
            commands.Add(new CommandContextItem(deepDive)
            {
                Title = Strings.DeepDiveTitle,
                Subtitle = Strings.DeepDiveSubtitle,
                Icon = AppIcons.Ai,
            });
        }

        var copyCommand = new CopyTextCommand(suggestion.TargetUri.AbsoluteUri)
        {
            Name = Strings.CommandCopyUrl,
            Icon = AppIcons.Copy,
            Result = CommandResult.ShowToast(new ToastArgs
            {
                Message = Strings.ToastUrlCopied,
                Result = CommandResult.KeepOpen(),
            }),
        };
        commands.Add(new CommandContextItem(copyCommand)
        {
            Title = Strings.CommandCopyUrl,
            Icon = AppIcons.Copy,
            RequestedShortcut = CopyShortcut,
        });

        return [.. commands];
    }

    private static bool CanShowDeepDive(SearchSuggestion suggestion, SearchPreferences preferences)
    {
        return preferences.EnableAiAnswerDetails &&
            !suggestion.IsNavigation &&
            suggestion.SourceKind is SuggestionSourceKind.SearchEngine or SuggestionSourceKind.SearchAnswer &&
            !string.IsNullOrWhiteSpace(suggestion.Query);
    }

    private static KeyChord? BuildProfileShortcut(int profileIndex)
    {
        // Map first nine profiles to Ctrl+Shift+1..9 for fast switching.
        if (profileIndex is < 1 or > 9)
        {
            return null;
        }

        const int vkey0 = 0x30; // virtual key code for '0'
        return KeyChordHelpers.FromModifiers(
            ctrl: true,
            alt: false,
            shift: true,
            win: false,
            vkey: vkey0 + profileIndex,
            scanCode: 0);
    }

    private static BrowserTarget? ResolveBrowserForExtraCommands()
    {
        var defaultKind = BrowserInstallDetector.DetectDefaultBrowserKind();
        if (defaultKind is null)
        {
            return null;
        }

        return BrowserInstallDetector.DetectInstalledBrowsers()
            .FirstOrDefault(browser => browser.Kind == defaultKind && !string.IsNullOrWhiteSpace(browser.ExecutablePath));
    }

    private static bool SupportsPrivateBrowsing(BrowserKind kind)
    {
        return kind is BrowserKind.Chrome or BrowserKind.Edge or BrowserKind.Brave or BrowserKind.Firefox;
    }

    private static string PrivateModeLabel(BrowserKind kind)
    {
        return kind switch
        {
            BrowserKind.Edge => Strings.CommandOpenInPrivate,
            BrowserKind.Firefox => Strings.CommandOpenInPrivateWindow,
            _ => Strings.CommandOpenInIncognito,
        };
    }

    private string? BuildFaviconImage(SearchSuggestion suggestion, SearchPreferences preferences)
    {
        if (!preferences.ShowFavicons ||
            (!suggestion.IsNavigation && suggestion.SourceKind is not (SuggestionSourceKind.DirectUrl or SuggestionSourceKind.BrowserBookmark or SuggestionSourceKind.BrowserHistory)))
        {
            return null;
        }

        return _faviconCacheService.GetCachedFaviconOrQueue(suggestion.TargetUri);
    }

    private static string BuildSubtitle(SearchSuggestion suggestion)
    {
        return suggestion.SourceKind switch
        {
            SuggestionSourceKind.DirectUrl => Strings.SubtitleOpenUrlDirectly,
            SuggestionSourceKind.SearchAnswer => suggestion.Description ?? string.Empty,
            SuggestionSourceKind.BrowserBookmark => Strings.SubtitleLocalBookmark(suggestion.BrowserName ?? string.Empty),
            SuggestionSourceKind.BrowserHistory => Strings.SubtitleLocalHistory(suggestion.BrowserName ?? string.Empty),
            _ when suggestion.IsNavigation => Strings.SubtitleNavigationSuggestion,
            _ => suggestion.Description ?? string.Empty,
        };
    }

    private static string ResolveSection(SearchSuggestion suggestion)
    {
        return suggestion.SourceKind switch
        {
            SuggestionSourceKind.BrowserBookmark => Strings.SectionBookmarks,
            SuggestionSourceKind.BrowserHistory => Strings.SectionHistory,
            SuggestionSourceKind.DirectUrl => Strings.SectionNavigation,
            SuggestionSourceKind.SearchAnswer => Strings.SectionAiAnswers,
            SuggestionSourceKind.SearchEngine when string.Equals(suggestion.Section, Strings.SectionRecentSearches, StringComparison.Ordinal)
                => Strings.SectionRecentSearches,
            SuggestionSourceKind.SearchEngine when string.Equals(suggestion.Section, Strings.SectionGoogleDefaultSuggestions, StringComparison.Ordinal)
                => Strings.SectionGoogleDefaultSuggestions,
            _ => Strings.SectionWeb,
        };
    }

    private static IconInfo BuildIcon(SearchSuggestion suggestion, string? image, SearchPreferences preferences)
    {
        if (!string.IsNullOrWhiteSpace(image))
        {
            return AppIcons.FromImageReference(image);
        }

        if (preferences.ShowFavicons && IsFaviconCandidate(suggestion))
        {
            return suggestion.SourceKind switch
            {
                SuggestionSourceKind.BrowserBookmark => AppIcons.Bookmark,
                SuggestionSourceKind.BrowserHistory => AppIcons.History,
                _ => AppIcons.Link,
            };
        }

        if (suggestion.IconHint?.Equals("calculator", StringComparison.OrdinalIgnoreCase) == true)
        {
            return AppIcons.Calculator;
        }

        if (suggestion.IconHint?.Equals("translate", StringComparison.OrdinalIgnoreCase) == true)
        {
            return AppIcons.Translate;
        }

        if (suggestion.IconHint?.Equals("dictionary", StringComparison.OrdinalIgnoreCase) == true)
        {
            return AppIcons.Dictionary;
        }

        if (suggestion.IconHint?.StartsWith("finance", StringComparison.OrdinalIgnoreCase) == true)
        {
            return ResolveFinanceIcon(suggestion);
        }

        if (suggestion.IconHint?.Equals("sports", StringComparison.OrdinalIgnoreCase) == true)
        {
            return AppIcons.Sports;
        }

        if (suggestion.IconHint?.StartsWith("weather", StringComparison.OrdinalIgnoreCase) == true)
        {
            return ResolveWeatherIcon(suggestion);
        }

        if (suggestion.IconHint?.Equals("currency", StringComparison.OrdinalIgnoreCase) == true)
        {
            return AppIcons.Currency;
        }

        if (suggestion.IconHint?.Equals("time", StringComparison.OrdinalIgnoreCase) == true)
        {
            return AppIcons.Time;
        }

        if (suggestion.IconHint?.Equals("local", StringComparison.OrdinalIgnoreCase) == true)
        {
            return AppIcons.Local;
        }

        if (suggestion.IconHint?.Equals("app", StringComparison.OrdinalIgnoreCase) == true)
        {
            return AppIcons.App;
        }

        if (suggestion.IconHint?.Equals("answer", StringComparison.OrdinalIgnoreCase) == true)
        {
            return AppIcons.Answer;
        }

        return suggestion.SourceKind switch
        {
            SuggestionSourceKind.DirectUrl => AppIcons.Link,
            SuggestionSourceKind.SearchAnswer => AppIcons.Search,
            SuggestionSourceKind.BrowserBookmark => AppIcons.Bookmark,
            SuggestionSourceKind.BrowserHistory => AppIcons.History,
            _ => AppIcons.Search,
        };
    }

    private LazySearchDetails? BuildDetails(SearchSuggestion suggestion, string? heroImage, SearchPreferences preferences)
    {
        if (suggestion.IsNavigation ||
            suggestion.SourceKind is SuggestionSourceKind.DirectUrl
                or SuggestionSourceKind.BrowserBookmark
                or SuggestionSourceKind.BrowserHistory)
        {
            return null;
        }

        var hasHeroImage = !string.IsNullOrWhiteSpace(heroImage);
        var hasMeaningfulDescription = HasMeaningfulDescription(suggestion);
        var enableExternalDetails = suggestion.IsCurrentQueryAction && preferences.EnableRichWebDetails;
        var allowAiAnswer = suggestion.IsCurrentQueryAction && preferences.EnableAiAnswerDetails;
        var hasAsyncCapability = enableExternalDetails || allowAiAnswer;

        var hasAsyncContent = false;
        if (hasAsyncCapability)
        {
            var cached = _richDetailsService.GetCachedMarkdownOrQueue(suggestion, preferences, allowAiAnswer);
            hasAsyncContent = !string.IsNullOrWhiteSpace(cached);
        }

        var willHaveTagMetadata = suggestion.IsCurrentQueryAction;

        if (!hasHeroImage && !hasMeaningfulDescription && !hasAsyncContent && !willHaveTagMetadata)
        {
            return null;
        }

        var markdown = new StringBuilder();
        if (hasMeaningfulDescription)
        {
            markdown.AppendLine(EscapeMarkdown(suggestion.Description!));
        }

        var details = new LazySearchDetails(
            _richDetailsService,
            suggestion,
            preferences,
            markdown.ToString(),
            enableExternalDetails: enableExternalDetails,
            allowAiAnswer: allowAiAnswer)
        {
            Title = suggestion.Title,
        };

        if (hasHeroImage)
        {
            details.HeroImage = AppIcons.FromImageReference(heroImage!);
        }

        return details;
    }

    private static bool HasMeaningfulDescription(SearchSuggestion suggestion)
    {
        var description = suggestion.Description;
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        if (description.Equals(suggestion.Title, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (description.Equals(suggestion.TargetUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase) ||
            description.Equals(suggestion.TargetUri.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !IsGenericDescription(description);
    }

    private static bool IsGenericDescription(string description)
    {
        return description.Equals(Strings.SubtitleDefaultSuggestion, StringComparison.OrdinalIgnoreCase) ||
            description.Equals(Strings.SubtitleRecentSearch, StringComparison.OrdinalIgnoreCase) ||
            description.Equals(Strings.SubtitleNavigationSuggestion, StringComparison.OrdinalIgnoreCase) ||
            description.Equals(Strings.SubtitleOpenUrlDirectly, StringComparison.OrdinalIgnoreCase) ||
            description.StartsWith(Strings.SuggestionSearchWithPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private void SetItems(IListItem[] items, bool clearSuggestions = false)
    {
        lock (_itemsLock)
        {
            if (clearSuggestions)
            {
                _displayedSuggestions = [];
                _displayedPreferences = null;
            }

            _items = items;
        }

        RaiseItemsChanged();
    }

    private void SetSuggestions(IReadOnlyList<SearchSuggestion> suggestions, SearchPreferences preferences)
    {
        var items = BuildListItems(suggestions, preferences);
        lock (_itemsLock)
        {
            _displayedSuggestions = suggestions;
            _displayedPreferences = preferences;
            _items = items;
        }

        RaiseItemsChanged();
    }

    private IListItem[] BuildListItems(IReadOnlyList<SearchSuggestion> suggestions, SearchPreferences preferences)
    {
        var filterId = preferences.ShowFilters ? _filters.CurrentFilterId : SuggestionFilterIds.All;
        var filterKind = SuggestionFilterIds.Parse(filterId);
        var filtered = suggestions.Where(suggestion => MatchesFilter(suggestion, filterKind)).ToArray();

        var orderedSectionIds = ParseSectionOrderIds(preferences.SectionOrder);
        var clusteredIds = new HashSet<string>(orderedSectionIds, StringComparer.Ordinal);

        var output = new List<IListItem>(filtered.Length + orderedSectionIds.Length);

        // 1. Items whose section is NOT in the order list keep their native (score-based) order
        // and are emitted first, interleaved with each other. No headers, no clustering.
        foreach (var suggestion in filtered)
        {
            if (!clusteredIds.Contains(SuggestionSectionId(suggestion)))
            {
                output.Add(BuildListItem(suggestion, preferences, applySection: false));
            }
        }

        // 2. Items whose section IS in the order list are clustered together below the mixed
        // results, in the order specified by the user. Within each clustered section we keep
        // the native score order from the input. A header item is emitted only when the
        // per-section header toggle is on.
        foreach (var sectionId in orderedSectionIds)
        {
            var sectionItems = filtered
                .Where(suggestion => SuggestionSectionId(suggestion).Equals(sectionId, StringComparison.Ordinal))
                .ToArray();
            if (sectionItems.Length == 0)
            {
                continue;
            }

            var showHeader = preferences.ShouldShowHeaderForSection(sectionId);
            if (showHeader)
            {
                var sectionTitle = ResolveSection(sectionItems[0]);
                if (!string.IsNullOrEmpty(sectionTitle))
                {
                    output.Add(BuildSectionHeader(sectionTitle));
                }
            }

            foreach (var suggestion in sectionItems)
            {
                output.Add(BuildListItem(suggestion, preferences, applySection: showHeader));
            }
        }

        return [.. output];
    }

    private static IListItem BuildSectionHeader(string title)
    {
        return new ListItem(new NoOpCommand())
        {
            Title = title,
            Section = title,
            Command = null!,
        };
    }

    private static string[] ParseSectionOrderIds(string sectionOrder)
    {
        if (string.IsNullOrWhiteSpace(sectionOrder))
        {
            return [];
        }

        return sectionOrder
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static id => id.ToLowerInvariant())
            .Where(static id => id.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string SuggestionSectionId(SearchSuggestion suggestion)
    {
        return suggestion.SourceKind switch
        {
            SuggestionSourceKind.DirectUrl => "navigation",
            SuggestionSourceKind.SearchAnswer => "answers",
            SuggestionSourceKind.BrowserBookmark => "bookmarks",
            SuggestionSourceKind.BrowserHistory => "history",
            SuggestionSourceKind.SearchEngine when string.Equals(suggestion.Section, Strings.SectionRecentSearches, StringComparison.Ordinal) => "recent",
            SuggestionSourceKind.SearchEngine when string.Equals(suggestion.Section, Strings.SectionGoogleDefaultSuggestions, StringComparison.Ordinal) => "trending",
            _ => "web",
        };
    }

    private static bool MatchesFilter(SearchSuggestion suggestion, SuggestionFilterKind filter)
    {
        return filter switch
        {
            SuggestionFilterKind.All => true,
            SuggestionFilterKind.Web => suggestion.SourceKind == SuggestionSourceKind.SearchEngine,
            SuggestionFilterKind.Local => suggestion.SourceKind is SuggestionSourceKind.BrowserBookmark or SuggestionSourceKind.BrowserHistory,
            SuggestionFilterKind.Answers => suggestion.SourceKind == SuggestionSourceKind.SearchAnswer,
            SuggestionFilterKind.Navigation => suggestion.SourceKind == SuggestionSourceKind.DirectUrl,
            _ => true,
        };
    }

    public void ClearCache()
    {
        _refreshCts.Cancel();
        _refreshCts.Dispose();
        _refreshCts = new CancellationTokenSource();
        _richDetailsService.ClearCache();
        _faviconCacheService.Clear();
        _suggestionFetchService.ClearCache();
        RecentSearchStore.Clear();
        PurgeImageCacheDirectory(_imageCacheDirectory);
        HideAiStatus();
        lock (_detailsTransitionRefreshed)
        {
            _detailsTransitionRefreshed.Clear();
        }

        lock (_itemsLock)
        {
            _displayedSuggestions = [];
            _displayedPreferences = null;
            _items = [];
        }

        EmptyContent = BuildEmptyContent(Strings.PageStartTyping, Strings.EmptyStateStartTypingSubtitle);
        RaiseItemsChanged();
    }

    private static void PurgeImageCacheDirectory(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                     .OrderByDescending(static p => p.Length))
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private void OnFaviconsChanged(object? sender, EventArgs e)
    {
        RefreshDisplayedSuggestions();
    }

    private void OnFilterPropertyChanged(object sender, IPropChangedEventArgs args)
    {
        if (string.Equals(args.PropertyName, nameof(SuggestionFilters.CurrentFilterId), StringComparison.Ordinal))
        {
            RefreshDisplayedSuggestions();
        }
    }

    private void OnAiStreamingStarted(object? sender, EventArgs e)
    {
        ShowAiStatus();
    }

    private void OnAiStreamingFinished(object? sender, EventArgs e)
    {
        HideAiStatus();
    }

    private void ShowAiStatus()
    {
        IExtensionHost? host;
        lock (_statusLock)
        {
            if (_aiStatusVisible)
            {
                return;
            }

            host = _extensionHost;
            if (host is null)
            {
                return;
            }

            _aiStatusVisible = true;
        }

        try
        {
            _ = host.ShowStatus(_aiStatusMessage, StatusContext.Page);
        }
        catch (InvalidOperationException)
        {
            lock (_statusLock)
            {
                _aiStatusVisible = false;
            }
        }
    }

    private void HideAiStatus()
    {
        IExtensionHost? host;
        lock (_statusLock)
        {
            if (!_aiStatusVisible)
            {
                return;
            }

            host = _extensionHost;
            _aiStatusVisible = false;
        }

        if (host is null)
        {
            return;
        }

        try
        {
            _ = host.HideStatus(_aiStatusMessage);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void OnRichDetailsChanged(object? sender, EventArgs e)
    {
        IReadOnlyList<SearchSuggestion> suggestions;
        SearchPreferences? preferences;
        lock (_itemsLock)
        {
            suggestions = _displayedSuggestions;
            preferences = _displayedPreferences;
        }

        if (preferences is null || suggestions.Count == 0)
        {
            return;
        }

        var refreshNeeded = preferences.RefreshListForLiveDetails;
        if (!refreshNeeded)
        {
            foreach (var suggestion in suggestions)
            {
                if (!suggestion.IsCurrentQueryAction)
                {
                    continue;
                }

                var allowAi = preferences.EnableAiAnswerDetails;
                if (!preferences.EnableRichWebDetails && !allowAi)
                {
                    continue;
                }

                var transitionKey = suggestion.Query;
                lock (_detailsTransitionRefreshed)
                {
                    if (_detailsTransitionRefreshed.Contains(transitionKey))
                    {
                        continue;
                    }
                }

                if (!_richDetailsService.HasResolvedContent(suggestion, preferences, allowAi))
                {
                    continue;
                }

                lock (_detailsTransitionRefreshed)
                {
                    _detailsTransitionRefreshed.Add(transitionKey);
                }

                refreshNeeded = true;
            }
        }

        if (refreshNeeded)
        {
            RefreshDisplayedSuggestions();
        }
    }

    private void RefreshDisplayedSuggestions()
    {
        IReadOnlyList<SearchSuggestion> suggestions;
        SearchPreferences? preferences;
        lock (_itemsLock)
        {
            suggestions = _displayedSuggestions;
            preferences = _displayedPreferences;
        }

        if (preferences is null || suggestions.Count == 0)
        {
            return;
        }

        SetSuggestions(suggestions, preferences);
    }

    private static CommandItem BuildEmptyContent(string title, string subtitle)
    {
        return new CommandItem(new NoOpCommand())
        {
            Title = title,
            Subtitle = subtitle,
            Icon = AppIcons.Search,
        };
    }

    private static IconInfo ResolveFinanceIcon(SearchSuggestion suggestion)
    {
        if (suggestion.IconHint is { } hint)
        {
            if (hint.Equals("finance.down", StringComparison.OrdinalIgnoreCase))
            {
                return AppIcons.FinanceDown;
            }

            if (hint.Equals("finance.up", StringComparison.OrdinalIgnoreCase))
            {
                return AppIcons.FinanceUp;
            }
        }

        return IsNegativeFinanceTrend(suggestion.Description) ? AppIcons.FinanceDown : AppIcons.FinanceUp;
    }

    private static IconInfo ResolveWeatherIcon(SearchSuggestion suggestion)
    {
        if (suggestion.IconHint is { } hint)
        {
            if (hint.Equals("weather.sunny", StringComparison.OrdinalIgnoreCase))
            {
                return AppIcons.WeatherSunny;
            }

            if (hint.Equals("weather.cloud", StringComparison.OrdinalIgnoreCase))
            {
                return AppIcons.WeatherCloud;
            }
        }

        return IsSunnyWeather(suggestion.Description) ? AppIcons.WeatherSunny : AppIcons.WeatherCloud;
    }

    private static bool IsNegativeFinanceTrend(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        if (description.Contains('▼') || description.Contains('↓'))
        {
            return true;
        }

        return HasNegativePercentageMarker(description);
    }

    private static bool HasNegativePercentageMarker(string description)
    {
        var percent = description.IndexOf('%');
        if (percent <= 0)
        {
            return false;
        }

        for (var i = percent - 1; i >= 0; i--)
        {
            var c = description[i];
            if (c is '-' or '−')
            {
                return true;
            }

            if (c is '+' or '▲' or '↑')
            {
                return false;
            }

            if (char.IsLetter(c))
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsSunnyWeather(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        var lowered = description.ToLowerInvariant();
        ReadOnlySpan<string> sunnyMarkers =
        [
            "sunny",
            "clear",
            "fair",
            "ensoleill",
            "dégag",
            "clair",
            "soleil",
        ];

        foreach (var marker in sunnyMarkers)
        {
            if (lowered.Contains(marker, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFaviconCandidate(SearchSuggestion suggestion)
    {
        return suggestion.IsNavigation ||
            suggestion.SourceKind is SuggestionSourceKind.DirectUrl or SuggestionSourceKind.BrowserBookmark or SuggestionSourceKind.BrowserHistory;
    }

    private static string EscapeMarkdown(string text)
    {
        return text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _refreshCts.Cancel();
        _refreshCts.Dispose();
        _faviconCacheService.FaviconsChanged -= OnFaviconsChanged;
        _richDetailsService.DetailsChanged -= OnRichDetailsChanged;
        _richDetailsService.AiStreamingStarted -= OnAiStreamingStarted;
        _richDetailsService.AiStreamingFinished -= OnAiStreamingFinished;
        _filters.PropChanged -= OnFilterPropertyChanged;
        HideAiStatus();
        _richDetailsService.Dispose();
        _faviconCacheService.Dispose();
        _httpClient.Dispose();
    }
}
