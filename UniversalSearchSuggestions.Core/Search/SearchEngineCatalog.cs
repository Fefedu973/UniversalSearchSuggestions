using UniversalSearchSuggestions.Core.Resources;

namespace UniversalSearchSuggestions.Core.Search;

public sealed record SearchEngineDefinition(
    SearchEngineKind Kind,
    string DisplayName,
    string SearchUrlTemplate,
    string SuggestionSection);

public static class SearchEngineCatalog
{
    public static readonly SearchEngineDefinition Google = Create(SearchEngineKind.Google, "Google", "https://www.google.com/search?q={query}");
    public static readonly SearchEngineDefinition Bing = Create(SearchEngineKind.Bing, "Bing", "https://www.bing.com/search?q={query}");
    public static readonly SearchEngineDefinition Yahoo = Create(SearchEngineKind.Yahoo, "Yahoo", "https://search.yahoo.com/search?p={query}");
    public static readonly SearchEngineDefinition Baidu = Create(SearchEngineKind.Baidu, "Baidu", "https://www.baidu.com/s?wd={query}");
    public static readonly SearchEngineDefinition Yandex = Create(SearchEngineKind.Yandex, "Yandex", "https://yandex.com/search/?text={query}");
    public static readonly SearchEngineDefinition DuckDuckGo = Create(SearchEngineKind.DuckDuckGo, "DuckDuckGo", "https://duckduckgo.com/?q={query}");
    public static readonly SearchEngineDefinition Naver = Create(SearchEngineKind.Naver, "Naver", "https://search.naver.com/search.naver?query={query}");
    public static readonly SearchEngineDefinition Ask = Create(SearchEngineKind.Ask, "Ask", "https://www.ask.com/web?q={query}");
    public static readonly SearchEngineDefinition Ecosia = Create(SearchEngineKind.Ecosia, "Ecosia", "https://www.ecosia.org/search?q={query}");
    public static readonly SearchEngineDefinition Brave = Create(SearchEngineKind.Brave, "Brave Search", "https://search.brave.com/search?q={query}");
    public static readonly SearchEngineDefinition Qwant = Create(SearchEngineKind.Qwant, "Qwant", "https://www.qwant.com/?q={query}");
    public static readonly SearchEngineDefinition Startpage = Create(SearchEngineKind.Startpage, "Startpage", "https://www.startpage.com/do/dsearch?query={query}");
    public static readonly SearchEngineDefinition Swisscows = Create(SearchEngineKind.Swisscows, "Swisscows", "https://swisscows.com/web?query={query}");
    public static readonly SearchEngineDefinition Dogpile = Create(SearchEngineKind.Dogpile, "Dogpile", "https://www.dogpile.com/serp?q={query}");
    public static readonly SearchEngineDefinition Gibiru = Create(SearchEngineKind.Gibiru, "Gibiru", "https://gibiru.com/results.html?q={query}");
    public static readonly SearchEngineDefinition Mojeek = Create(SearchEngineKind.Mojeek, "Mojeek", "https://www.mojeek.com/search?q={query}");
    public static readonly SearchEngineDefinition MetaGer = Create(SearchEngineKind.MetaGer, "MetaGer", "https://metager.org/meta/meta.ger3?eingabe={query}");
    public static readonly SearchEngineDefinition ZapMeta = Create(SearchEngineKind.ZapMeta, "ZapMeta", "https://www.zapmeta.com/search?q={query}");
    public static readonly SearchEngineDefinition SearchEncrypt = Create(SearchEngineKind.SearchEncrypt, "Search Encrypt", "https://www.searchencrypt.com/search?q={query}");
    public static readonly SearchEngineDefinition OneSearch = Create(SearchEngineKind.OneSearch, "OneSearch", "https://www.onesearch.com/yhs/search?q={query}");
    public static readonly SearchEngineDefinition Ekoru = Create(SearchEngineKind.Ekoru, "Ekoru", "https://ekoru.org/search?q={query}");

    public static IReadOnlyList<SearchEngineDefinition> BuiltInEngines { get; } =
    [
        Google,
        Bing,
        Yahoo,
        Baidu,
        Yandex,
        DuckDuckGo,
        Naver,
        Ask,
        Ecosia,
        Brave,
        Qwant,
        Startpage,
        Swisscows,
        Dogpile,
        Gibiru,
        Mojeek,
        MetaGer,
        ZapMeta,
        SearchEncrypt,
        OneSearch,
        Ekoru,
    ];

    public static SearchEngineDefinition Get(SearchEngineKind engine)
    {
        return BuiltInEngines.FirstOrDefault(definition => definition.Kind == engine) ??
            (engine == SearchEngineKind.Custom
                ? new SearchEngineDefinition(
                    SearchEngineKind.Custom,
                    Strings.SearchEngineCustom,
                    "{query}",
                    Strings.SearchEngineCustom)
                : Google);
    }

    public static Uri BuildSearchUri(SearchEngineKind engine, string query, string? customTemplate = null)
    {
        var template = engine == SearchEngineKind.Custom && !string.IsNullOrWhiteSpace(customTemplate)
            ? customTemplate!
            : Get(engine).SearchUrlTemplate;

        return BuildFromTemplate(template, query);
    }

    public static Uri BuildFromTemplate(string template, string query)
    {
        var encoded = Uri.EscapeDataString(query.Trim());
        var encodedPlus = encoded.Replace("%20", "+", StringComparison.Ordinal);
        var raw = template
            .Replace("{query}", encoded, StringComparison.OrdinalIgnoreCase)
            .Replace("{query+}", encodedPlus, StringComparison.OrdinalIgnoreCase)
            .Replace("%s", encoded, StringComparison.OrdinalIgnoreCase);

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            return new Uri(Google.SearchUrlTemplate.Replace("{query}", encoded, StringComparison.OrdinalIgnoreCase));
        }

        return uri;
    }

    public static bool TryParseEngine(string value, out SearchEngineKind engine)
    {
        return Enum.TryParse(value, ignoreCase: true, out engine);
    }

    private static SearchEngineDefinition Create(SearchEngineKind kind, string displayName, string searchUrlTemplate)
    {
        return new SearchEngineDefinition(kind, displayName, searchUrlTemplate, displayName);
    }
}
