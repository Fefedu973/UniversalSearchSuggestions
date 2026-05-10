using System.Collections.Concurrent;
using UniversalSearchSuggestions.Core.Browsers;
using UniversalSearchSuggestions.Core.Resources;
using UniversalSearchSuggestions.Core.Search.Providers;
using UniversalSearchSuggestions.Core.Utilities;

namespace UniversalSearchSuggestions.Core.Search;

public sealed class SuggestionFetchService(
    IReadOnlyList<ISuggestionProvider> providers,
    HttpClient? httpClient = null)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan EmptySearchCacheDuration = TimeSpan.FromMinutes(30);
    private static readonly HttpClient DefaultHttpClient = new();
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CacheEntry> _emptySearchCache = new(StringComparer.Ordinal);
    private readonly HttpClient _httpClient = httpClient ?? DefaultHttpClient;

    public void ClearCache()
    {
        _cache.Clear();
        _emptySearchCache.Clear();
    }

    public Task<IReadOnlyList<SearchSuggestion>> SearchAsync(
        string rawQuery,
        SearchPreferences preferences,
        CancellationToken cancellationToken)
    {
        return SearchAsync(rawQuery, preferences, [], cancellationToken);
    }

    public async Task<IReadOnlyList<SearchSuggestion>> SearchAsync(
        string rawQuery,
        SearchPreferences preferences,
        IReadOnlyList<string> recentQueries,
        CancellationToken cancellationToken)
    {
        var query = rawQuery.Trim();
        if (query.Length == 0)
        {
            return [];
        }

        var cacheKey = BuildCacheKey(query, preferences, recentQueries);
        if (_cache.TryGetValue(cacheKey, out var cached) && DateTimeOffset.UtcNow - cached.CreatedAt < CacheDuration)
        {
            return cached.Suggestions;
        }

        var results = BuildImmediateSuggestions(query, preferences).ToList();

        if (preferences.IncludeRecentInSearchResults && recentQueries.Count > 0)
        {
            results.AddRange(BuildMatchingRecentSuggestions(recentQueries, query, preferences));
        }

        var providerTasks = providers
            .Where(provider => preferences.IsEngineEnabled(provider.Engine))
            .Select(provider => SafeFetchProviderAsync(provider, query, preferences, cancellationToken))
            .ToList();

        if (preferences.IncludeBrowserBookmarks || preferences.IncludeBrowserHistory)
        {
            providerTasks.Add(SafeFetchLocalAsync(query, preferences, cancellationToken));
        }

        var fetched = await Task.WhenAll(providerTasks).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        results.AddRange(fetched.SelectMany(items => items.Select(item => NormalizeOpenTarget(item, preferences))));
        var merged = Merge(results, preferences.MaxTotalResults);
        _cache[cacheKey] = new CacheEntry(DateTimeOffset.UtcNow, merged);
        return merged;
    }

    private static IEnumerable<SearchSuggestion> BuildMatchingRecentSuggestions(
        IReadOnlyList<string> recentQueries,
        string query,
        SearchPreferences preferences)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var emitted = 0;
        foreach (var raw in recentQueries)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var trimmed = raw.Trim();
            if (trimmed.Equals(query, StringComparison.OrdinalIgnoreCase) || !seen.Add(trimmed))
            {
                continue;
            }

            if (trimmed.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            yield return new SearchSuggestion
            {
                Title = trimmed,
                Query = trimmed,
                TargetUri = BuildPreferredSearchUri(trimmed, preferences),
                Engine = preferences.PrimaryEngine,
                SourceKind = SuggestionSourceKind.SearchEngine,
                Description = Strings.SubtitleRecentSearch,
                Section = Strings.SectionRecentSearches,
                TextToSuggest = trimmed,
                IconHint = "time",
                Score = 145 - emitted,
            };

            emitted++;
            if (emitted >= 6)
            {
                yield break;
            }
        }
    }

    public static IReadOnlyList<SearchSuggestion> BuildImmediateSuggestions(
        string rawQuery,
        SearchPreferences preferences)
    {
        var query = rawQuery.Trim();
        if (query.Length == 0)
        {
            return [];
        }

        var results = new List<SearchSuggestion>();
        if (UrlInputParser.TryParse(query, out var directUri))
        {
            results.Add(BuildDirectUrlSuggestion(query, directUri));
        }

        results.Add(BuildSearchCurrentQuerySuggestion(query, preferences));
        return results;
    }

    public async Task<IReadOnlyList<SearchSuggestion>> GetEmptySearchSuggestionsAsync(
        SearchPreferences preferences,
        IReadOnlyList<string> recentQueries,
        CancellationToken cancellationToken)
    {
        if (preferences.EmptySearchSuggestionsMode == EmptySearchSuggestionsMode.None)
        {
            return [];
        }

        var cacheKey = BuildEmptySearchCacheKey(preferences, recentQueries);
        if (_emptySearchCache.TryGetValue(cacheKey, out var cached) &&
            DateTimeOffset.UtcNow - cached.CreatedAt < EmptySearchCacheDuration)
        {
            return cached.Suggestions;
        }

        var suggestions = new List<SearchSuggestion>();
        if (preferences.EmptySearchSuggestionsMode is EmptySearchSuggestionsMode.RecentSearches or EmptySearchSuggestionsMode.RecentAndGoogleDefault)
        {
            suggestions.AddRange(BuildRecentSearchSuggestions(recentQueries, preferences));
        }

        if (preferences.EmptySearchSuggestionsMode is EmptySearchSuggestionsMode.GoogleDefault or EmptySearchSuggestionsMode.RecentAndGoogleDefault)
        {
            suggestions.AddRange(await FetchGoogleDefaultSuggestionsAsync(preferences, cancellationToken).ConfigureAwait(false));
        }

        var merged = Merge(suggestions, preferences.MaxTotalResults);
        _emptySearchCache[cacheKey] = new CacheEntry(DateTimeOffset.UtcNow, merged);
        return merged;
    }

    private static async Task<IReadOnlyList<SearchSuggestion>> SafeFetchProviderAsync(
        ISuggestionProvider provider,
        string query,
        SearchPreferences preferences,
        CancellationToken cancellationToken)
    {
        try
        {
            return await provider.GetSuggestionsAsync(query, preferences, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
        catch (System.Xml.XmlException)
        {
            return [];
        }
        catch (InvalidOperationException)
        {
            return [];
        }
    }

    private static async Task<IReadOnlyList<SearchSuggestion>> SafeFetchLocalAsync(
        string query,
        SearchPreferences preferences,
        CancellationToken cancellationToken)
    {
        try
        {
            return await LocalBrowserSuggestionsProvider.SearchAsync(query, preferences, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static SearchSuggestion BuildDirectUrlSuggestion(string query, Uri uri)
    {
        return new SearchSuggestion
        {
            Title = Strings.SuggestionOpen(UrlInputParser.DisplayHost(uri)),
            Query = query,
            TargetUri = uri,
            Engine = SearchEngineKind.Custom,
            SourceKind = SuggestionSourceKind.DirectUrl,
            Description = uri.ToString(),
            Section = Strings.SectionNavigation,
            TextToSuggest = uri.Host,
            Score = 200,
            IsNavigation = true,
        };
    }

    private static SearchSuggestion BuildSearchCurrentQuerySuggestion(string query, SearchPreferences preferences)
    {
        var engine = preferences.PrimaryEngine;
        var target = BuildPreferredSearchUri(query, preferences);

        return new SearchSuggestion
        {
            Title = query,
            Query = query,
            TargetUri = target,
            Engine = engine,
            SourceKind = SuggestionSourceKind.SearchEngine,
            Section = Strings.SectionSearch,
            TextToSuggest = query,
            Score = 150,
            IsCurrentQueryAction = true,
        };
    }

    private static SearchSuggestion NormalizeOpenTarget(SearchSuggestion suggestion, SearchPreferences preferences)
    {
        if (suggestion.IsNavigation ||
            suggestion.SourceKind is SuggestionSourceKind.DirectUrl or SuggestionSourceKind.BrowserBookmark or SuggestionSourceKind.BrowserHistory)
        {
            return suggestion;
        }

        return suggestion with
        {
            TargetUri = BuildPreferredSearchUri(suggestion.Query, preferences),
        };
    }

    public static Uri BuildPreferredSearchUri(string query, SearchPreferences preferences)
    {
        return preferences.PrimaryEngine == SearchEngineKind.Custom
            ? SearchEngineCatalog.BuildSearchUri(SearchEngineKind.Custom, query, preferences.CustomSearchUrlTemplate)
            : SearchEngineCatalog.BuildSearchUri(preferences.PrimaryEngine, query);
    }

    private static SearchSuggestion[] BuildRecentSearchSuggestions(
        IReadOnlyList<string> recentQueries,
        SearchPreferences preferences)
    {
        return recentQueries
            .Where(static query => !string.IsNullOrWhiteSpace(query))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(preferences.MaxTotalResults)
            .Select((query, index) => new SearchSuggestion
            {
                Title = query,
                Query = query,
                TargetUri = BuildPreferredSearchUri(query, preferences),
                Engine = preferences.PrimaryEngine,
                SourceKind = SuggestionSourceKind.SearchEngine,
                Description = Strings.SubtitleRecentSearch,
                Section = Strings.SectionRecentSearches,
                TextToSuggest = query,
                IconHint = "time",
                Score = 135 - index,
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<SearchSuggestion>> FetchGoogleDefaultSuggestionsAsync(
        SearchPreferences preferences,
        CancellationToken cancellationToken)
    {
        try
        {
            var (hl, gl) = DefaultSuggestionProviders.SplitLanguageTag(preferences.Language);
            var host = ResolveGoogleHost(gl);
            var uri = string.Concat(
                $"https://{host}/complete/s?q=&cp=0&client=gws-wiz&xssi=t&gs_pcrt=2",
                $"&hl={Uri.EscapeDataString(hl)}",
                $"&gl={Uri.EscapeDataString(gl.ToLowerInvariant())}",
                "&authuser=0",
                $"&psi={Guid.NewGuid():N}.{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                "&dpr=1&nolsbt=1");
            var payload = await _httpClient.GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
            return GoogleSuggestionParser.ParseDefaultSuggestions(payload, preferences);
        }
        catch (HttpRequestException)
        {
            return [];
        }
        catch (InvalidOperationException)
        {
            return [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    private static string ResolveGoogleHost(string region)
    {
        return region.ToLowerInvariant() switch
        {
            "us" => "www.google.com",
            "gb" or "uk" => "www.google.co.uk",
            "au" => "www.google.com.au",
            "br" => "www.google.com.br",
            "mx" => "www.google.com.mx",
            "jp" => "www.google.co.jp",
            "kr" => "www.google.co.kr",
            "fr" => "www.google.fr",
            "ca" => "www.google.ca",
            "de" => "www.google.de",
            "es" => "www.google.es",
            "it" => "www.google.it",
            "nl" => "www.google.nl",
            "pl" => "www.google.pl",
            "se" => "www.google.se",
            "dk" => "www.google.dk",
            "no" => "www.google.no",
            "fi" => "www.google.fi",
            _ => "www.google.com",
        };
    }

    private static List<SearchSuggestion> Merge(IEnumerable<SearchSuggestion> suggestions, int maxCount)
    {
        var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var results = new List<SearchSuggestion>();

        foreach (var suggestion in suggestions.OrderByDescending(static item => item.Score))
        {
            var key = BuildMergeKey(suggestion);
            if (indexes.TryGetValue(key, out var existingIndex))
            {
                results[existingIndex] = EnrichDuplicate(results[existingIndex], suggestion);
                continue;
            }

            if (results.Count >= maxCount)
            {
                continue;
            }

            indexes[key] = results.Count;
            results.Add(suggestion);
        }

        return results;
    }

    private static string BuildMergeKey(SearchSuggestion suggestion)
    {
        return suggestion.SourceKind is SuggestionSourceKind.DirectUrl or SuggestionSourceKind.BrowserBookmark or SuggestionSourceKind.BrowserHistory
            ? suggestion.TargetUri.ToString()
            : suggestion.Query;
    }

    private static SearchSuggestion EnrichDuplicate(SearchSuggestion existing, SearchSuggestion incoming)
    {
        return existing with
        {
            Title = SelectRicherTitle(existing, incoming),
            SourceKind = SelectRicherSourceKind(existing, incoming),
            Description = SelectRicherValue(existing.Description, incoming.Description, existing),
            ImageUrl = SelectRicherValue(existing.ImageUrl, incoming.ImageUrl, existing),
            TextToSuggest = string.IsNullOrWhiteSpace(existing.TextToSuggest) ? incoming.TextToSuggest : existing.TextToSuggest,
            Section = string.IsNullOrWhiteSpace(existing.Section) ? incoming.Section : existing.Section,
            IconHint = string.IsNullOrWhiteSpace(existing.IconHint) ? incoming.IconHint : existing.IconHint,
            IsCurrentQueryAction = existing.IsCurrentQueryAction || incoming.IsCurrentQueryAction,
        };
    }

    private static SuggestionSourceKind SelectRicherSourceKind(SearchSuggestion existing, SearchSuggestion incoming)
    {
        return existing.IsCurrentQueryAction && incoming.SourceKind == SuggestionSourceKind.SearchAnswer
            ? SuggestionSourceKind.SearchAnswer
            : existing.SourceKind;
    }

    private static string SelectRicherTitle(SearchSuggestion existing, SearchSuggestion incoming)
    {
        if (existing.IsCurrentQueryAction &&
            !string.IsNullOrWhiteSpace(incoming.Title) &&
            !incoming.Title.Equals(existing.Title, StringComparison.Ordinal) &&
            !incoming.Title.Equals(Strings.SuggestionSearch(existing.Query), StringComparison.OrdinalIgnoreCase))
        {
            return incoming.Title;
        }

        return existing.Title;
    }

    private static string? SelectRicherValue(string? existingValue, string? incomingValue, SearchSuggestion existing)
    {
        if (string.IsNullOrWhiteSpace(incomingValue))
        {
            return existingValue;
        }

        if (string.IsNullOrWhiteSpace(existingValue) ||
            existingValue.StartsWith(Strings.SuggestionSearchWithPrefix, StringComparison.OrdinalIgnoreCase) ||
            existing.SourceKind == SuggestionSourceKind.SearchEngine && incomingValue.Length > existingValue.Length)
        {
            return incomingValue;
        }

        return existingValue;
    }

    private static string BuildCacheKey(string query, SearchPreferences preferences, IReadOnlyList<string> recentQueries)
    {
        var recentFingerprint = preferences.IncludeRecentInSearchResults && recentQueries.Count > 0
            ? string.Join('', recentQueries.Take(10))
            : string.Empty;

        return string.Join(
            '|',
            query.ToLowerInvariant(),
            preferences.PrimaryEngine,
            preferences.EnableGoogle,
            preferences.EnableBing,
            preferences.EnableYahoo,
            preferences.EnableDuckDuckGo,
            preferences.EnableEcosia,
            preferences.EnableBrave,
            preferences.EnableQwant,
            preferences.EnableSwisscows,
            preferences.EnableGoogleRichSuggestions,
            preferences.EnableGoogleOmniboxAnswers,
            preferences.EnableGoogleToolbarSuggestions,
            preferences.IncludeBrowserBookmarks,
            preferences.IncludeBrowserHistory,
            preferences.BrowserId,
            preferences.LocalBrowserId,
            preferences.MaxSuggestionsPerEngine,
            preferences.MaxLocalResults,
            preferences.MaxTotalResults,
            preferences.EnableRichWebDetails,
            preferences.RichDetailsEndpointTemplate,
            preferences.EnableAiAnswerDetails,
            preferences.AiAnswerEndpointTemplate,
            preferences.AiAnswerModel,
            preferences.Language,
            preferences.CustomSearchUrlTemplate,
            preferences.IncludeRecentInSearchResults,
            recentFingerprint);
    }

    private static string BuildEmptySearchCacheKey(
        SearchPreferences preferences,
        IReadOnlyList<string> recentQueries)
    {
        return string.Join(
            '|',
            "empty",
            preferences.EmptySearchSuggestionsMode,
            preferences.PrimaryEngine,
            preferences.CustomSearchUrlTemplate,
            preferences.Language,
            preferences.MaxTotalResults,
            string.Join('\u001f', recentQueries.Take(10)));
    }

    private sealed record CacheEntry(DateTimeOffset CreatedAt, IReadOnlyList<SearchSuggestion> Suggestions);
}
