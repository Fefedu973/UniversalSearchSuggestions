using System.Text;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using UniversalSearchSuggestions.Commands;
using UniversalSearchSuggestions.Core.Search;
using UniversalSearchSuggestions.Core.Search.Providers;
using UniversalSearchSuggestions.Core.Utilities;
using UniversalSearchSuggestions.Icons;
using UniversalSearchSuggestions.RichDetails;
using UniversalSearchSuggestions.Settings;

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
    private IListItem[] _items = [BuildInfoItem("Commencez à taper pour chercher.")];
    private IReadOnlyList<SearchSuggestion> _displayedSuggestions = [];
    private SearchPreferences? _displayedPreferences;

    public UniversalSearchSuggestionsPage(SearchSettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
        _httpClient = SearchHttpClientFactory.Create();
        _suggestionFetchService = new SuggestionFetchService(
            DefaultSuggestionProviders.Create(_httpClient));
        _imageCacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UniversalSearchSuggestions",
            "ImageCache");
        _faviconCacheService = new FaviconCacheService(
            _httpClient,
            Path.Combine(_imageCacheDirectory, "Favicons"));
        _faviconCacheService.FaviconsChanged += OnFaviconsChanged;
        _richDetailsService = new RichDetailsService(_httpClient);
        _richDetailsService.DetailsChanged += OnRichDetailsChanged;

        Icon = AppIcons.ExtensionLogo;
        Title = "Universal Search";
        Name = "Universal Search";
        PlaceholderText = "Recherche, URL ou favori...";
        ShowDetails = true;
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

        var query = newSearch.Trim();
        if (query.Length == 0)
        {
            IsLoading = false;
            SetItems([BuildInfoItem("Commencez à taper pour chercher.")], clearSuggestions: true);
            return;
        }

        var preferences = _settingsManager.Snapshot();
        ShowDetails = preferences.ShowDetails;
        SetSuggestions(SuggestionFetchService.BuildImmediateSuggestions(query, preferences), preferences);

        IsLoading = true;
        _ = RefreshAsync(query, version, _refreshCts.Token);
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
                SetItems([BuildInfoItem($"Aucune suggestion pour \"{query}\".")], clearSuggestions: true);
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
            preferences.DecodeDataImages);
        image ??= BuildFaviconImage(suggestion, preferences);

        return new ListItem(new OpenSearchTargetCommand(suggestion, preferences))
        {
            Title = suggestion.Title,
            Subtitle = BuildSubtitle(suggestion),
            Icon = BuildIcon(suggestion, image, preferences),
            Details = preferences.ShowDetails ? BuildDetails(suggestion, image, preferences) : null,
            TextToSuggest = preferences.EnableSearchBoxAutocomplete ? suggestion.TextToSuggest ?? suggestion.Query : string.Empty,
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
            SuggestionSourceKind.DirectUrl => "Ouvrir l'URL directement",
            SuggestionSourceKind.SearchAnswer => $"Réponse Google Omnibox - {suggestion.Description ?? "réponse"}",
            SuggestionSourceKind.BrowserBookmark => $"Favori local - {suggestion.BrowserName}",
            SuggestionSourceKind.BrowserHistory => $"Historique local - {suggestion.BrowserName}",
            _ when suggestion.IsNavigation => "Navigation suggérée",
            _ => $"{SearchEngineCatalog.Get(suggestion.Engine).DisplayName} - {suggestion.Description ?? "Suggestion"}",
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
            return AppIcons.Link;
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

        if (suggestion.IconHint?.Equals("finance", StringComparison.OrdinalIgnoreCase) == true)
        {
            return AppIcons.Finance;
        }

        if (suggestion.IconHint?.Equals("sports", StringComparison.OrdinalIgnoreCase) == true)
        {
            return AppIcons.Sports;
        }

        if (suggestion.IconHint?.Equals("weather", StringComparison.OrdinalIgnoreCase) == true)
        {
            return AppIcons.Weather;
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

    private LazySearchDetails BuildDetails(SearchSuggestion suggestion, string? image, SearchPreferences preferences)
    {
        var markdown = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(image))
        {
            markdown.Append("![");
            markdown.Append(EscapeMarkdown(suggestion.Title));
            markdown.Append("](");
            markdown.Append(image);
            markdown.AppendLine(")");
            markdown.AppendLine();
        }

        markdown.Append("### ");
        markdown.AppendLine(EscapeMarkdown(suggestion.Title));
        markdown.AppendLine();

        if (!string.IsNullOrWhiteSpace(suggestion.Description))
        {
            markdown.AppendLine(EscapeMarkdown(suggestion.Description));
            markdown.AppendLine();
        }

        markdown.Append("**Source**: ");
        markdown.AppendLine(EscapeMarkdown(BuildSourceLabel(suggestion)));
        markdown.Append("**Action**: ");
        markdown.AppendLine(EscapeMarkdown(suggestion.IsNavigation ? "ouvrir directement" : $"chercher avec {SearchEngineCatalog.Get(preferences.PrimaryEngine).DisplayName}"));
        markdown.AppendLine();
        markdown.Append('[');
        markdown.Append(EscapeMarkdown(ToDisplayUri(suggestion.TargetUri)));
        markdown.Append("](");
        markdown.Append(ToDisplayUri(suggestion.TargetUri));
        markdown.AppendLine(")");

        if (preferences.PrimaryEngine == SearchEngineKind.Custom)
        {
            markdown.AppendLine();
            markdown.Append("URL système: ");
            markdown.Append(EscapeMarkdown(preferences.CustomSearchUrlTemplate));
        }

        return new LazySearchDetails(
            _richDetailsService,
            suggestion,
            preferences,
            markdown.ToString(),
            enableExternalDetails: suggestion.IsCurrentQueryAction,
            allowAiAnswer: suggestion.IsCurrentQueryAction);
    }

    private static string BuildSourceLabel(SearchSuggestion suggestion)
    {
        return suggestion.SourceKind switch
        {
            SuggestionSourceKind.DirectUrl => "URL détectée",
            SuggestionSourceKind.SearchAnswer => "Réponse Google Omnibox",
            SuggestionSourceKind.BrowserBookmark => $"Favori {suggestion.BrowserName}",
            SuggestionSourceKind.BrowserHistory => $"Historique {suggestion.BrowserName}",
            _ => SearchEngineCatalog.Get(suggestion.Engine).DisplayName,
        };
    }

    private static string ToDisplayUri(Uri uri)
    {
        return uri.Scheme is "http" or "https" ? uri.AbsoluteUri : uri.ToString();
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
            new Separator("Navigateur local"),
            .. local.Select(suggestion => BuildListItem(suggestion, preferences)),
        ];
    }

    private void OnFaviconsChanged(object? sender, EventArgs e)
    {
        RefreshDisplayedSuggestions();
    }

    private void OnRichDetailsChanged(object? sender, EventArgs e)
    {
        SearchPreferences? preferences;
        lock (_itemsLock)
        {
            preferences = _displayedPreferences;
        }

        if (preferences?.RefreshListForLiveDetails == true)
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
