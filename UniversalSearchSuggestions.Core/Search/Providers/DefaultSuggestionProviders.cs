namespace UniversalSearchSuggestions.Core.Search.Providers;

public static class DefaultSuggestionProviders
{
    public static IReadOnlyList<ISuggestionProvider> Create(HttpClient httpClient)
    {
        return
        [
            new GoogleSuggestionProvider(httpClient),
            new JsonArraySuggestionProvider(
                SearchEngineKind.Bing,
                httpClient,
                static (query, language) => new Uri($"https://api.bing.com/osjson.aspx?query={Uri.EscapeDataString(query)}&language={Uri.EscapeDataString(NormalizeBingLanguage(language))}")),
            new JsonArraySuggestionProvider(
                SearchEngineKind.Yahoo,
                httpClient,
                static (query, _) => new Uri($"https://sugg.search.yahoo.net/sg/?output=json&nresults=10&command={Uri.EscapeDataString(query)}")),
            new JsonArraySuggestionProvider(
                SearchEngineKind.DuckDuckGo,
                httpClient,
                static (query, _) => new Uri($"https://duckduckgo.com/ac/?q={Uri.EscapeDataString(query)}&type=list")),
            new JsonArraySuggestionProvider(
                SearchEngineKind.Ecosia,
                httpClient,
                static (query, _) => new Uri($"https://ac.ecosia.org/?q={Uri.EscapeDataString(query)}")),
            new JsonArraySuggestionProvider(
                SearchEngineKind.Brave,
                httpClient,
                static (query, _) => new Uri($"https://search.brave.com/api/suggest?rich=true&q={Uri.EscapeDataString(query)}")),
            new JsonArraySuggestionProvider(
                SearchEngineKind.Qwant,
                httpClient,
                static (query, _) => new Uri($"https://api.qwant.com/v3/suggest?q={Uri.EscapeDataString(query)}")),
            new JsonArraySuggestionProvider(
                SearchEngineKind.Swisscows,
                httpClient,
                static (query, _) => new Uri($"https://api.swisscows.com/suggest?query={Uri.EscapeDataString(query)}")),
        ];
    }

    private static string NormalizeBingLanguage(string language)
    {
        return language.Contains('-', StringComparison.Ordinal)
            ? language
            : language.Equals("fr", StringComparison.OrdinalIgnoreCase) ? "fr-FR" : "en-US";
    }
}
