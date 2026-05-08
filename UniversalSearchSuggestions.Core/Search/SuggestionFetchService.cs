using System.Collections.Concurrent;
using UniversalSearchSuggestions.Core.Browsers;
using UniversalSearchSuggestions.Core.Search.Providers;
using UniversalSearchSuggestions.Core.Utilities;

namespace UniversalSearchSuggestions.Core.Search;

public sealed class SuggestionFetchService(
    IReadOnlyList<ISuggestionProvider> providers)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);

    public async Task<IReadOnlyList<SearchSuggestion>> SearchAsync(
        string rawQuery,
        SearchPreferences preferences,
        CancellationToken cancellationToken)
    {
        var query = rawQuery.Trim();
        if (query.Length == 0)
        {
            return [];
        }

        var cacheKey = BuildCacheKey(query, preferences);
        if (_cache.TryGetValue(cacheKey, out var cached) && DateTimeOffset.UtcNow - cached.CreatedAt < CacheDuration)
        {
            return cached.Suggestions;
        }

        var results = BuildImmediateSuggestions(query, preferences).ToList();

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
            Title = $"Ouvrir {UrlInputParser.DisplayHost(uri)}",
            Query = query,
            TargetUri = uri,
            Engine = SearchEngineKind.Custom,
            SourceKind = SuggestionSourceKind.DirectUrl,
            Description = uri.ToString(),
            Section = "Navigation",
            TextToSuggest = uri.Host,
            Score = 200,
            IsNavigation = true,
        };
    }

    private static SearchSuggestion BuildSearchCurrentQuerySuggestion(string query, SearchPreferences preferences)
    {
        var engine = preferences.PrimaryEngine;
        var target = BuildPreferredSearchUri(query, preferences);
        var definition = SearchEngineCatalog.Get(engine);

        return new SearchSuggestion
        {
            Title = $"Rechercher \"{query}\"",
            Query = query,
            TargetUri = target,
            Engine = engine,
            SourceKind = SuggestionSourceKind.SearchEngine,
            Description = $"Recherche avec {definition.DisplayName}",
            Section = "Recherche",
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

    private static Uri BuildPreferredSearchUri(string query, SearchPreferences preferences)
    {
        return preferences.PrimaryEngine == SearchEngineKind.Custom
            ? SearchEngineCatalog.BuildSearchUri(SearchEngineKind.Custom, query, preferences.CustomSearchUrlTemplate)
            : SearchEngineCatalog.BuildSearchUri(preferences.PrimaryEngine, query);
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
            Description = SelectRicherValue(existing.Description, incoming.Description, existing),
            ImageUrl = SelectRicherValue(existing.ImageUrl, incoming.ImageUrl, existing),
            TextToSuggest = string.IsNullOrWhiteSpace(existing.TextToSuggest) ? incoming.TextToSuggest : existing.TextToSuggest,
            Section = string.IsNullOrWhiteSpace(existing.Section) ? incoming.Section : existing.Section,
            IconHint = string.IsNullOrWhiteSpace(existing.IconHint) ? incoming.IconHint : existing.IconHint,
            IsCurrentQueryAction = existing.IsCurrentQueryAction || incoming.IsCurrentQueryAction,
        };
    }

    private static string? SelectRicherValue(string? existingValue, string? incomingValue, SearchSuggestion existing)
    {
        if (string.IsNullOrWhiteSpace(incomingValue))
        {
            return existingValue;
        }

        if (string.IsNullOrWhiteSpace(existingValue) ||
            existingValue.StartsWith("Recherche avec ", StringComparison.OrdinalIgnoreCase) ||
            existing.SourceKind == SuggestionSourceKind.SearchEngine && incomingValue.Length > existingValue.Length)
        {
            return incomingValue;
        }

        return existingValue;
    }

    private static string BuildCacheKey(string query, SearchPreferences preferences)
    {
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
            preferences.CustomSearchUrlTemplate);
    }

    private sealed record CacheEntry(DateTimeOffset CreatedAt, IReadOnlyList<SearchSuggestion> Suggestions);
}
