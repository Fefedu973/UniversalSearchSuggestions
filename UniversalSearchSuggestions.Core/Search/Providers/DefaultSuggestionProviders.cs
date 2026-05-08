using System.Globalization;

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
                static (query, language) => new Uri(
                    $"https://api.bing.com/osjson.aspx?query={Uri.EscapeDataString(query)}" +
                    $"&language={Uri.EscapeDataString(BuildLanguageRegionTag(language))}" +
                    $"&market={Uri.EscapeDataString(BuildLanguageRegionTag(language))}")),
            new JsonArraySuggestionProvider(
                SearchEngineKind.Yahoo,
                httpClient,
                static (query, language) =>
                {
                    var (lang, region) = SplitLanguageTag(language);
                    return new Uri(
                        $"https://sugg.search.yahoo.net/sg/?output=json&nresults=10" +
                        $"&command={Uri.EscapeDataString(query)}" +
                        $"&appid=ymsgr" +
                        $"&intl={Uri.EscapeDataString(region.ToLowerInvariant())}" +
                        $"&lang={Uri.EscapeDataString(lang)}");
                }),
            new JsonArraySuggestionProvider(
                SearchEngineKind.DuckDuckGo,
                httpClient,
                static (query, language) =>
                {
                    var (lang, region) = SplitLanguageTag(language);
                    return new Uri(
                        $"https://duckduckgo.com/ac/?q={Uri.EscapeDataString(query)}&type=list" +
                        $"&kl={Uri.EscapeDataString($"{region.ToLowerInvariant()}-{lang}")}");
                }),
            new JsonArraySuggestionProvider(
                SearchEngineKind.Ecosia,
                httpClient,
                static (query, language) => new Uri(
                    $"https://ac.ecosia.org/?q={Uri.EscapeDataString(query)}" +
                    $"&mkt={Uri.EscapeDataString(BuildLanguageRegionTag(language).ToLowerInvariant())}")),
            new JsonArraySuggestionProvider(
                SearchEngineKind.Brave,
                httpClient,
                static (query, language) =>
                {
                    var (lang, region) = SplitLanguageTag(language);
                    return new Uri(
                        $"https://search.brave.com/api/suggest?rich=true" +
                        $"&q={Uri.EscapeDataString(query)}" +
                        $"&country={Uri.EscapeDataString(region.ToLowerInvariant())}" +
                        $"&lang={Uri.EscapeDataString(lang)}");
                }),
            new JsonArraySuggestionProvider(
                SearchEngineKind.Qwant,
                httpClient,
                static (query, language) =>
                {
                    var (lang, region) = SplitLanguageTag(language);
                    return new Uri(
                        $"https://api.qwant.com/v3/suggest?q={Uri.EscapeDataString(query)}" +
                        $"&locale={Uri.EscapeDataString($"{lang}_{region}")}");
                }),
            new JsonArraySuggestionProvider(
                SearchEngineKind.Swisscows,
                httpClient,
                static (query, language) =>
                {
                    var (lang, region) = SplitLanguageTag(language);
                    return new Uri(
                        $"https://api.swisscows.com/suggest?query={Uri.EscapeDataString(query)}" +
                        $"&region={Uri.EscapeDataString($"{lang}-{region}")}" +
                        $"&itemsCount=10");
                }),
        ];
    }

    internal static (string Language, string Region) SplitLanguageTag(string languageTag)
    {
        var parts = string.IsNullOrWhiteSpace(languageTag)
            ? Array.Empty<string>()
            : languageTag.Split('-', '_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var language = parts.Length > 0 ? parts[0].ToLowerInvariant() : string.Empty;
        var region = parts.Length > 1 ? parts[1].ToUpperInvariant() : string.Empty;

        if (string.IsNullOrEmpty(language))
        {
            var fallback = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            language = string.IsNullOrWhiteSpace(fallback) || fallback.Equals("iv", StringComparison.OrdinalIgnoreCase)
                ? "en"
                : fallback.ToLowerInvariant();
        }

        if (string.IsNullOrEmpty(region))
        {
            try
            {
                region = RegionInfo.CurrentRegion.TwoLetterISORegionName.ToUpperInvariant();
            }
            catch (InvalidOperationException)
            {
                region = "US";
            }

            if (string.IsNullOrEmpty(region))
            {
                region = language switch
                {
                    "en" => "US",
                    "fr" => "FR",
                    "es" => "ES",
                    "de" => "DE",
                    "it" => "IT",
                    "pt" => "PT",
                    "nl" => "NL",
                    "ja" => "JP",
                    "ko" => "KR",
                    "zh" => "CN",
                    "ru" => "RU",
                    _ => "US",
                };
            }
        }

        return (language, region);
    }

    internal static string BuildLanguageRegionTag(string languageTag)
    {
        var (language, region) = SplitLanguageTag(languageTag);
        return $"{language}-{region}";
    }
}
