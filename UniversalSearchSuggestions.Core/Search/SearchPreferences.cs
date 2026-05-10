using System.Globalization;

namespace UniversalSearchSuggestions.Core.Search;

public sealed record SearchPreferences
{
    public SearchEngineKind PrimaryEngine { get; init; } = SearchEngineKind.Google;

    public string BrowserId { get; init; } = "default";

    public string CustomBrowserPath { get; init; } = string.Empty;

    public string CustomSearchUrlTemplate { get; init; } = "https://www.google.com/search?q={query}";

    public string Language { get; init; } = DefaultLanguageTag();

    public bool EnableGoogle { get; init; } = true;

    public bool EnableBing { get; init; }

    public bool EnableYahoo { get; init; }

    public bool EnableDuckDuckGo { get; init; }

    public bool EnableEcosia { get; init; }

    public bool EnableBrave { get; init; }

    public bool EnableQwant { get; init; }

    public bool EnableSwisscows { get; init; }

    public bool EnableGoogleRichSuggestions { get; init; } = true;

    public bool EnableGoogleOmniboxAnswers { get; init; } = true;

    public bool EnableGoogleToolbarSuggestions { get; init; }

    public bool IncludeBrowserBookmarks { get; init; }

    public bool IncludeBrowserHistory { get; init; }

    public string LocalBrowserId { get; init; } = "same";

    public bool ShowDetails { get; init; } = true;

    public bool EnableRichWebDetails { get; init; }

    public string RichDetailsEndpointTemplate { get; init; } = string.Empty;

    public bool EnableAiAnswerDetails { get; init; }

    public bool RefreshListForLiveDetails { get; init; }

    public bool EnableAiAnswerDebug { get; init; }

    public string AiAnswerEndpointTemplate { get; init; } = "https://oai.endpoints.kepler.ai.cloud.ovh.net/v1/chat/completions";

    public string AiAnswerModel { get; init; } = "Llama-3.1-8B-Instruct";

    public string AiAnswerApiKey { get; init; } = string.Empty;

    public bool EnableSearchBoxAutocomplete { get; init; } = true;

    public EmptySearchSuggestionsMode EmptySearchSuggestionsMode { get; init; }

    public bool ShowFavicons { get; init; } = true;

    public bool GroupLocalBrowserResults { get; init; } = true;

    public bool ShowHeaderWeb { get; init; } = true;

    public bool ShowHeaderBookmarks { get; init; } = true;

    public bool ShowHeaderHistory { get; init; } = true;

    public bool ShowHeaderAnswers { get; init; } = true;

    public bool ShowHeaderNavigation { get; init; } = true;

    public bool ShowHeaderRecent { get; init; } = true;

    public bool ShowHeaderTrending { get; init; } = true;

    public bool ShowResultTags { get; init; } = true;

    public bool ShowFilters { get; init; } = true;

    public string SectionOrder { get; init; } = DefaultSectionOrder;

    public bool IncludeRecentInSearchResults { get; init; }

    public const string DefaultSectionOrder = "navigation,answers,recent,trending,web,bookmarks,history";

    public bool ShouldShowHeaderForSection(string sectionId)
    {
        return sectionId switch
        {
            "web" => ShowHeaderWeb,
            "bookmarks" => ShowHeaderBookmarks,
            "history" => ShowHeaderHistory,
            "answers" => ShowHeaderAnswers,
            "navigation" => ShowHeaderNavigation,
            "recent" => ShowHeaderRecent,
            "trending" => ShowHeaderTrending,
            _ => false,
        };
    }

    public bool DecodeDataImages { get; init; } = true;

    public int MaxSuggestionsPerEngine { get; init; } = 5;

    public int MaxLocalResults { get; init; } = 5;

    public int MaxTotalResults { get; init; } = 18;

    public int DebounceMilliseconds { get; init; } = 110;

    public bool IsEngineEnabled(SearchEngineKind engine)
    {
        return engine switch
        {
            SearchEngineKind.Google => EnableGoogle,
            SearchEngineKind.Bing => EnableBing,
            SearchEngineKind.Yahoo => EnableYahoo,
            SearchEngineKind.DuckDuckGo => EnableDuckDuckGo,
            SearchEngineKind.Ecosia => EnableEcosia,
            SearchEngineKind.Brave => EnableBrave,
            SearchEngineKind.Qwant => EnableQwant,
            SearchEngineKind.Swisscows => EnableSwisscows,
            SearchEngineKind.Custom => false,
            _ => false,
        };
    }

    private static string DefaultLanguageTag()
    {
        var culture = CultureInfo.CurrentCulture.Name;
        return string.IsNullOrWhiteSpace(culture) ? "en-US" : culture;
    }
}
