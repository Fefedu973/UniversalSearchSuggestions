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
    private readonly SearchSettingsManager _settingsManager;
    private readonly HttpClient _httpClient;
    private readonly SuggestionFetchService _suggestionFetchService;
    private readonly FaviconCacheService _faviconCacheService;
    private readonly RichDetailsService _richDetailsService;
    private readonly string _imageCacheDirectory;
    private readonly object _itemsLock = new();

    private CancellationTokenSource _refreshCts = new();
    private long _requestVersion;
    private IListItem[] _items = [BuildInfoItem(Strings.PageStartTyping)];
    private IReadOnlyList<SearchSuggestion> _displayedSuggestions = [];
    private SearchPreferences? _displayedPreferences;
    private readonly HashSet<string> _detailsTransitionRefreshed = new(StringComparer.Ordinal);

    public UniversalSearchSuggestionsPage(SearchSettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
        _httpClient = SearchHttpClientFactory.Create();
        _suggestionFetchService = new SuggestionFetchService(
            DefaultSuggestionProviders.Create(_httpClient),
            _httpClient);
        _imageCacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UniversalSearchSuggestions",
            "ImageCache");
        _faviconCacheService = new FaviconCacheService(
            _httpClient,
            Path.Combine(_imageCacheDirectory, "Favicons"));
        _faviconCacheService.FaviconsChanged += OnFaviconsChanged;
        _richDetailsService = new RichDetailsService(_httpClient, Path.Combine(_imageCacheDirectory, "RichDetails"));
        _richDetailsService.DetailsChanged += OnRichDetailsChanged;

        Icon = AppIcons.ExtensionLogo;
        Title = "Universal Search";
        Name = "Universal Search";
        PlaceholderText = Strings.PagePlaceholder;
        ShowDetails = true;

        var preferences = _settingsManager.Snapshot();
        if (preferences.EmptySearchSuggestionsMode != EmptySearchSuggestionsMode.None)
        {
            var version = Interlocked.Increment(ref _requestVersion);
            IsLoading = true;
            _ = RefreshEmptySearchAsync(version, preferences, _refreshCts.Token);
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
        lock (_detailsTransitionRefreshed)
        {
            _detailsTransitionRefreshed.Clear();
        }

        var query = newSearch.Trim();
        var preferences = _settingsManager.Snapshot();
        ShowDetails = preferences.ShowDetails;

        if (query.Length == 0)
        {
            if (preferences.EmptySearchSuggestionsMode == EmptySearchSuggestionsMode.None)
            {
                IsLoading = false;
                SetItems([BuildInfoItem(Strings.PageStartTyping)], clearSuggestions: true);
                return;
            }

            SetItems([BuildInfoItem(Strings.PageStartTyping)], clearSuggestions: true);
            IsLoading = true;
            _ = RefreshEmptySearchAsync(version, preferences, _refreshCts.Token);
            return;
        }

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

            if (suggestions.Count == 0)
            {
                SetItems([BuildInfoItem(Strings.PageStartTyping)], clearSuggestions: true);
            }
            else
            {
                SetSuggestions(suggestions, preferences);
            }
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

            var suggestions = await _suggestionFetchService
                .SearchAsync(query, preferences, cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            if (version != Interlocked.Read(ref _requestVersion))
            {
                return;
            }

            if (suggestions.Count == 0)
            {
                SetItems([BuildInfoItem(Strings.PageNoSuggestions(query))], clearSuggestions: true);
            }
            else
            {
                SetSuggestions(suggestions, preferences);
            }
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

    private ListItem BuildListItem(SearchSuggestion suggestion, SearchPreferences preferences)
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
            MoreCommands = BuildMoreCommands(suggestion, preferences),
            TextToSuggest = preferences.EnableSearchBoxAutocomplete ? suggestion.TextToSuggest ?? suggestion.Query : string.Empty,
        };
    }

    private static IContextItem[] BuildMoreCommands(SearchSuggestion suggestion, SearchPreferences preferences)
    {
        var commands = new List<IContextItem>();
        var browser = ResolveBrowserForExtraCommands();

        if (browser is not null)
        {
            var profiles = BrowserProfileDetector.Detect(browser);
            foreach (var profile in profiles)
            {
                if (profile.IsDefault)
                {
                    continue;
                }

                var openWithProfile = new OpenSearchTargetCommand(
                    suggestion,
                    preferences,
                    browser,
                    profile,
                    privateMode: false,
                    nameOverride: Strings.CommandOpenInProfile(profile.DisplayName));
                commands.Add(new CommandContextItem(openWithProfile)
                {
                    Title = Strings.CommandOpenInProfile(profile.DisplayName),
                    Icon = AppIcons.Profile,
                });
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
                });
            }
        }

        commands.Add(new CommandContextItem(new CopyTextCommand(suggestion.TargetUri.AbsoluteUri))
        {
            Title = Strings.CommandCopyUrl,
            Icon = AppIcons.Copy,
        });

        return [.. commands];
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

        if (!hasHeroImage && !hasMeaningfulDescription && !hasAsyncContent)
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
        if (!preferences.GroupLocalBrowserResults)
        {
            return suggestions.Select(suggestion => BuildListItem(suggestion, preferences)).ToArray<IListItem>();
        }

        var nonLocal = suggestions.Where(static suggestion => !IsLocalBrowserSuggestion(suggestion)).ToArray();
        var local = suggestions.Where(static suggestion => IsLocalBrowserSuggestion(suggestion)).ToArray();
        if (nonLocal.Length == 0 || local.Length == 0)
        {
            return suggestions.Select(suggestion => BuildListItem(suggestion, preferences)).ToArray<IListItem>();
        }

        return
        [
            .. nonLocal.Select(suggestion => BuildListItem(suggestion, preferences)),
            new Separator(Strings.PageLocalBrowserSeparator),
            .. local.Select(suggestion => BuildListItem(suggestion, preferences)),
        ];
    }

    private void OnFaviconsChanged(object? sender, EventArgs e)
    {
        RefreshDisplayedSuggestions();
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

    private static ListItem BuildInfoItem(string text)
    {
        return new ListItem(new NoOpCommand())
        {
            Title = text,
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

    private static bool IsLocalBrowserSuggestion(SearchSuggestion suggestion)
    {
        return suggestion.SourceKind is SuggestionSourceKind.BrowserBookmark or SuggestionSourceKind.BrowserHistory;
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
        _richDetailsService.Dispose();
        _faviconCacheService.Dispose();
        _httpClient.Dispose();
    }
}
