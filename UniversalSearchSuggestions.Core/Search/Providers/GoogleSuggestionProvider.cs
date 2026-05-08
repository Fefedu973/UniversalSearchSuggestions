using System.Text;
using System.Globalization;

namespace UniversalSearchSuggestions.Core.Search.Providers;

public sealed class GoogleSuggestionProvider(HttpClient httpClient) : ISuggestionProvider
{
    private readonly string _sessionToken = Guid.NewGuid().ToString("N");

    public SearchEngineKind Engine => SearchEngineKind.Google;

    public async Task<IReadOnlyList<SearchSuggestion>> GetSuggestionsAsync(
        string query,
        SearchPreferences preferences,
        CancellationToken cancellationToken)
    {
        var tasks = new List<Task<IReadOnlyList<SearchSuggestion>>>
        {
            FetchChromeAsync(query, preferences.Language, cancellationToken),
        };

        if (preferences.EnableGoogleRichSuggestions)
        {
            tasks.Add(FetchRichAsync(query, preferences.Language, cancellationToken));
        }

        if (preferences.EnableGoogleOmniboxAnswers)
        {
            tasks.Add(FetchOmniboxAnswersAsync(query, preferences.Language, cancellationToken));
        }

        if (preferences.EnableGoogleToolbarSuggestions)
        {
            tasks.Add(FetchToolbarAsync(query, preferences.Language, cancellationToken));
        }

        var resultSets = await Task.WhenAll(tasks).ConfigureAwait(false);
        return MergeGoogleResults(resultSets.SelectMany(static result => result), preferences.MaxSuggestionsPerEngine);
    }

    private async Task<IReadOnlyList<SearchSuggestion>> FetchRichAsync(
        string query,
        string language,
        CancellationToken cancellationToken)
    {
        var uri = $"https://www.google.com/complete/search?client=gws-wiz&hl={Uri.EscapeDataString(language)}&q={Uri.EscapeDataString(query)}";
        var payload = await httpClient.GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
        return GoogleSuggestionParser.ParseRichSuggestions(payload, query);
    }

    private async Task<IReadOnlyList<SearchSuggestion>> FetchChromeAsync(
        string query,
        string language,
        CancellationToken cancellationToken)
    {
        var uri = $"https://suggestqueries.google.com/complete/search?client=chrome&hl={Uri.EscapeDataString(language)}&q={Uri.EscapeDataString(query)}";
        var payload = await httpClient.GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
        return GoogleSuggestionParser.ParseChromeSuggestions(payload, query);
    }

    private async Task<IReadOnlyList<SearchSuggestion>> FetchOmniboxAnswersAsync(
        string query,
        string language,
        CancellationToken cancellationToken)
    {
        var chromiumUri = BuildChromiumOmniboxUri(query, language, _sessionToken);
        var simpleUri = BuildSimpleChromeOmniboxUri(query, language);
        var resultSets = await Task.WhenAll(
            FetchOmniboxEndpointAsync(chromiumUri, query, cancellationToken),
            FetchOmniboxEndpointAsync(simpleUri, query, cancellationToken)).ConfigureAwait(false);

        return MergeGoogleResults(resultSets.SelectMany(static result => result), maxCount: 6);
    }

    private async Task<IReadOnlyList<SearchSuggestion>> FetchToolbarAsync(
        string query,
        string language,
        CancellationToken cancellationToken)
    {
        var uri = $"https://www.google.com/complete/search?output=toolbar&hl={Uri.EscapeDataString(language)}&q={Uri.EscapeDataString(query)}";
        var payload = await httpClient.GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
        return GoogleSuggestionParser.ParseToolbarSuggestions(payload);
    }

    private async Task<IReadOnlyList<SearchSuggestion>> FetchOmniboxEndpointAsync(
        Uri uri,
        string query,
        CancellationToken cancellationToken)
    {
        var payload = await httpClient.GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
        return GoogleSuggestionParser.ParseChromeOmniboxAnswerSuggestions(payload, query);
    }

    private static Uri BuildChromiumOmniboxUri(string query, string language, string sessionToken)
    {
        var (hl, gl) = NormalizeGoogleLanguageAndRegion(language);
        var uri = new UriBuilder("https", ResolveGoogleHost(gl), -1, "/complete/search")
        {
            Query = string.Join(
                '&',
                $"client=chrome-omni",
                $"gs_ri=chrome-ext-ansg",
                $"xssi=t",
                $"q={Uri.EscapeDataString(query)}",
                $"oit=4",
                $"cp={query.EnumerateRunes().Count()}",
                $"pgcl=4",
                $"gs_rn=42",
                $"psi={Uri.EscapeDataString(sessionToken)}",
                $"hl={Uri.EscapeDataString(hl)}",
                $"gl={Uri.EscapeDataString(gl)}",
                $"ie=UTF-8",
                $"oe=UTF-8"),
        };

        return uri.Uri;
    }

    private static Uri BuildSimpleChromeOmniboxUri(string query, string language)
    {
        var (hl, gl) = NormalizeGoogleLanguageAndRegion(language);
        var uri = new UriBuilder("https", ResolveGoogleHost(gl), -1, "/complete/search")
        {
            Query = string.Join(
                '&',
                $"client=chrome",
                $"xssi=t",
                $"q={Uri.EscapeDataString(query)}",
                $"hl={Uri.EscapeDataString(hl)}",
                $"gl={Uri.EscapeDataString(gl)}",
                $"ie=UTF-8",
                $"oe=UTF-8"),
        };

        return uri.Uri;
    }

    private static (string Language, string Region) NormalizeGoogleLanguageAndRegion(string language)
    {
        var parts = language.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var cultureParts = (CultureInfo.CurrentCulture.Name.Length > 0 ? CultureInfo.CurrentCulture.Name : "en-US")
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hl = parts.Length > 0 ? parts[0].ToLowerInvariant() : cultureParts[0].ToLowerInvariant();
        var gl = parts.Length > 1 ? parts[1].ToLowerInvariant() : RegionInfo.CurrentRegion.TwoLetterISORegionName.ToLowerInvariant();
        return (hl, gl);
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
            "cn" => "www.google.com",
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

    private static List<SearchSuggestion> MergeGoogleResults(
        IEnumerable<SearchSuggestion> suggestions,
        int maxCount)
    {
        var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var merged = new List<SearchSuggestion>();

        foreach (var suggestion in suggestions.OrderByDescending(static item => item.Score))
        {
            var key = suggestion.IsNavigation ? suggestion.TargetUri.ToString() : suggestion.Query;
            if (indexes.TryGetValue(key, out var existingIndex))
            {
                merged[existingIndex] = EnrichDuplicate(merged[existingIndex], suggestion);
                continue;
            }

            if (merged.Count >= maxCount)
            {
                continue;
            }

            indexes[key] = merged.Count;
            merged.Add(suggestion);
        }

        return merged;
    }

    private static SearchSuggestion EnrichDuplicate(SearchSuggestion existing, SearchSuggestion incoming)
    {
        return existing with
        {
            Description = SelectRicherValue(existing.Description, incoming.Description),
            ImageUrl = SelectRicherValue(existing.ImageUrl, incoming.ImageUrl),
            TextToSuggest = string.IsNullOrWhiteSpace(existing.TextToSuggest) ? incoming.TextToSuggest : existing.TextToSuggest,
            IconHint = string.IsNullOrWhiteSpace(existing.IconHint) ? incoming.IconHint : existing.IconHint,
        };
    }

    private static string? SelectRicherValue(string? existingValue, string? incomingValue)
    {
        if (string.IsNullOrWhiteSpace(incomingValue))
        {
            return existingValue;
        }

        if (string.IsNullOrWhiteSpace(existingValue) || incomingValue.Length > existingValue.Length)
        {
            return incomingValue;
        }

        return existingValue;
    }
}
