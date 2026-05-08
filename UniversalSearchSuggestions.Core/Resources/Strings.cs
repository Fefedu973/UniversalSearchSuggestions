using System.Globalization;
using System.Resources;

namespace UniversalSearchSuggestions.Core.Resources;

public static class Strings
{
    private static readonly ResourceManager Manager = new(
        "UniversalSearchSuggestions.Core.Resources.Strings",
        typeof(Strings).Assembly);

    private static CultureInfo? _culture;

    public static CultureInfo Culture
    {
        get => _culture ?? CultureInfo.CurrentUICulture;
        set => _culture = value;
    }

    public static string Get(string key) => Manager.GetString(key, Culture) ?? key;

    public static string Format(string key, params object?[] arguments) =>
        string.Format(Culture, Get(key), arguments);

    public static string PagePlaceholder => Get("Page_Placeholder");
    public static string PageStartTyping => Get("Page_StartTyping");
    public static string PageNoSuggestions(string query) => Format("Page_NoSuggestions_Format", query);
    public static string PageLocalBrowserSeparator => Get("Page_LocalBrowserSeparator");

    public static string SubtitleOpenUrlDirectly => Get("Subtitle_OpenUrlDirectly");
    public static string SubtitleGoogleOmniboxAnswer(string description) => Format("Subtitle_GoogleOmniboxAnswer_Format", description);
    public static string SubtitleLocalBookmark(string browser) => Format("Subtitle_LocalBookmark_Format", browser);
    public static string SubtitleLocalHistory(string browser) => Format("Subtitle_LocalHistory_Format", browser);
    public static string SubtitleNavigationSuggestion => Get("Subtitle_NavigationSuggestion");
    public static string SubtitleEngineSuggestion(string engine, string description) => Format("Subtitle_EngineSuggestion_Format", engine, description);
    public static string SubtitleDefaultSuggestion => Get("Subtitle_DefaultSuggestion");
    public static string SubtitleRecentSearch => Get("Subtitle_RecentSearch");

    public static string SourceDetectedUrl => Get("Source_DetectedUrl");
    public static string SourceGoogleOmnibox => Get("Source_GoogleOmnibox");
    public static string SourceLocalBookmark(string browser) => Format("Source_LocalBookmark_Format", browser);
    public static string SourceLocalHistory(string browser) => Format("Source_LocalHistory_Format", browser);

    public static string DetailSource => Get("Detail_Source");
    public static string DetailAction => Get("Detail_Action");
    public static string DetailOpenDirectly => Get("Detail_OpenDirectly");
    public static string DetailSearchWith(string engine) => Format("Detail_SearchWith_Format", engine);
    public static string DetailSystemUrlPrefix => Get("Detail_SystemUrl_Prefix");

    public static string SectionBookmarks => Get("Section_Bookmarks");
    public static string SectionHistory => Get("Section_History");
    public static string SectionSearch => Get("Section_Search");
    public static string SectionNavigation => Get("Section_Navigation");
    public static string SectionGoogleAnswers => Get("Section_GoogleAnswers");
    public static string SectionRecentSearches => Get("Section_RecentSearches");
    public static string SectionGoogleDefaultSuggestions => Get("Section_GoogleDefaultSuggestions");

    public static string SuggestionOpen(string host) => Format("Suggestion_Open_Format", host);
    public static string SuggestionSearch(string query) => Format("Suggestion_Search_Format", query);
    public static string SuggestionSearchWith(string engine) => Format("Suggestion_SearchWith_Format", engine);
    public static string SuggestionSearchWithPrefix => Get("Suggestion_SearchWith_Prefix");
    public static string SearchEngineCustom => Get("SearchEngine_Custom");
    public static string BrowserDefault => Get("Browser_Default");
    public static string BrowserCustomPath => Get("Browser_CustomPath");
    public static string BrowserCustom => Get("Browser_Custom");

    public static string CommandOpen => Get("Command_Open");
    public static string CommandSearch => Get("Command_Search");
    public static string CommandOpenInProfile(string profileName) => Format("Command_OpenInProfile_Format", profileName);
    public static string CommandOpenInPrivate => Get("Command_OpenInPrivate");
    public static string CommandOpenInIncognito => Get("Command_OpenInIncognito");
    public static string CommandOpenInPrivateWindow => Get("Command_OpenInPrivateWindow");
    public static string CommandCopyUrl => Get("Command_CopyUrl");

    public static string AnswerTypeDictionary => Get("AnswerType_Dictionary");
    public static string AnswerTypeFinance => Get("AnswerType_Finance");
    public static string AnswerTypeGeneric => Get("AnswerType_Generic");
    public static string AnswerTypeLocal => Get("AnswerType_Local");
    public static string AnswerTypeSports => Get("AnswerType_Sports");
    public static string AnswerTypeSunriseSunset => Get("AnswerType_SunriseSunset");
    public static string AnswerTypeTranslation => Get("AnswerType_Translation");
    public static string AnswerTypeWeather => Get("AnswerType_Weather");
    public static string AnswerTypeWhenIs => Get("AnswerType_WhenIs");
    public static string AnswerTypeCurrency => Get("AnswerType_Currency");
    public static string AnswerTypeLocalTime => Get("AnswerType_LocalTime");
    public static string AnswerTypePlayInstall => Get("AnswerType_PlayInstall");
    public static string AnswerTypeCalculator => Get("AnswerType_Calculator");

    public static string RichDetailsHeaderEnrichedResult => Get("RichDetails_Header_EnrichedResult");
    public static string RichDetailsHeaderAnswerSingular => Get("RichDetails_Header_AnswerSingular");
    public static string RichDetailsHeaderAnswerPlural => Get("RichDetails_Header_AnswerPlural");
    public static string RichDetailsHeaderInfobox => Get("RichDetails_Header_Infobox");
    public static string RichDetailsHeaderKnowledgeGraph => Get("RichDetails_Header_KnowledgeGraph");
    public static string RichDetailsHeaderWebDetails => Get("RichDetails_Header_WebDetails");
    public static string RichDetailsHeaderWikipedia => Get("RichDetails_Header_Wikipedia");
    public static string RichDetailsHeaderAiAnswer => Get("RichDetails_Header_AiAnswer");
    public static string RichDetailsLoading => Get("RichDetails_Loading");
    public static string RichDetailsReadOnWikipedia => Get("RichDetails_ReadOnWikipedia");
    public static string RichDetailsSource => Get("RichDetails_Source");
    public static string RichDetailsAiPromptLanguage => Get("RichDetails_AiPromptLanguage");
    public static string RichDetailsAiPrompt(string language, string query) => Format("RichDetails_AiPrompt_Format", language, query);

    public static string SettingsPrimaryEngineLabel => Get("Settings_PrimaryEngine_Label");
    public static string SettingsPrimaryEngineDescription => Get("Settings_PrimaryEngine_Description");
    public static string SettingsCustomUrlChoice => Get("Settings_CustomUrlChoice");
    public static string SettingsOpeningBrowserLabel => Get("Settings_OpeningBrowser_Label");
    public static string SettingsOpeningBrowserDescription => Get("Settings_OpeningBrowser_Description");
    public static string SettingsLocalBrowserLabel => Get("Settings_LocalBrowser_Label");
    public static string SettingsLocalBrowserDescription => Get("Settings_LocalBrowser_Description");
    public static string SettingsLocalBrowserSameAsOpening => Get("Settings_LocalBrowser_SameAsOpening");
    public static string SettingsCustomBrowserPathLabel => Get("Settings_CustomBrowserPath_Label");
    public static string SettingsCustomBrowserPathDescription => Get("Settings_CustomBrowserPath_Description");
    public static string SettingsCustomSearchUrlLabel => Get("Settings_CustomSearchUrl_Label");
    public static string SettingsCustomSearchUrlDescription => Get("Settings_CustomSearchUrl_Description");
    public static string SettingsLanguageLabel => Get("Settings_Language_Label");
    public static string SettingsLanguageDescription => Get("Settings_Language_Description");

    public static string SettingsGoogleLabel => Get("Settings_Google_Label");
    public static string SettingsGoogleDescription => Get("Settings_Google_Description");
    public static string SettingsBingLabel => Get("Settings_Bing_Label");
    public static string SettingsBingDescription => Get("Settings_Bing_Description");
    public static string SettingsYahooLabel => Get("Settings_Yahoo_Label");
    public static string SettingsYahooDescription => Get("Settings_Yahoo_Description");
    public static string SettingsDuckDuckGoLabel => Get("Settings_DuckDuckGo_Label");
    public static string SettingsDuckDuckGoDescription => Get("Settings_DuckDuckGo_Description");
    public static string SettingsEcosiaLabel => Get("Settings_Ecosia_Label");
    public static string SettingsEcosiaDescription => Get("Settings_Ecosia_Description");
    public static string SettingsBraveLabel => Get("Settings_Brave_Label");
    public static string SettingsBraveDescription => Get("Settings_Brave_Description");
    public static string SettingsQwantLabel => Get("Settings_Qwant_Label");
    public static string SettingsQwantDescription => Get("Settings_Qwant_Description");
    public static string SettingsSwisscowsLabel => Get("Settings_Swisscows_Label");
    public static string SettingsSwisscowsDescription => Get("Settings_Swisscows_Description");
    public static string SettingsGoogleRichLabel => Get("Settings_GoogleRich_Label");
    public static string SettingsGoogleRichDescription => Get("Settings_GoogleRich_Description");
    public static string SettingsGoogleOmniboxLabel => Get("Settings_GoogleOmnibox_Label");
    public static string SettingsGoogleOmniboxDescription => Get("Settings_GoogleOmnibox_Description");
    public static string SettingsGoogleToolbarLabel => Get("Settings_GoogleToolbar_Label");
    public static string SettingsGoogleToolbarDescription => Get("Settings_GoogleToolbar_Description");
    public static string SettingsBookmarksLabel => Get("Settings_Bookmarks_Label");
    public static string SettingsBookmarksDescription => Get("Settings_Bookmarks_Description");
    public static string SettingsHistoryLabel => Get("Settings_History_Label");
    public static string SettingsHistoryDescription => Get("Settings_History_Description");
    public static string SettingsDetailsLabel => Get("Settings_Details_Label");
    public static string SettingsDetailsDescription => Get("Settings_Details_Description");
    public static string SettingsRichWebDetailsLabel => Get("Settings_RichWebDetails_Label");
    public static string SettingsRichWebDetailsDescription => Get("Settings_RichWebDetails_Description");
    public static string SettingsRichDetailsEndpointLabel => Get("Settings_RichDetailsEndpoint_Label");
    public static string SettingsRichDetailsEndpointDescription => Get("Settings_RichDetailsEndpoint_Description");
    public static string SettingsAiAnswerLabel => Get("Settings_AiAnswer_Label");
    public static string SettingsAiAnswerDescription => Get("Settings_AiAnswer_Description");
    public static string SettingsLiveDetailsRefreshLabel => Get("Settings_LiveDetailsRefresh_Label");
    public static string SettingsLiveDetailsRefreshDescription => Get("Settings_LiveDetailsRefresh_Description");
    public static string SettingsAiEndpointLabel => Get("Settings_AiEndpoint_Label");
    public static string SettingsAiEndpointDescription => Get("Settings_AiEndpoint_Description");
    public static string SettingsAiModelLabel => Get("Settings_AiModel_Label");
    public static string SettingsAiModelDescription => Get("Settings_AiModel_Description");
    public static string SettingsAiApiKeyLabel => Get("Settings_AiApiKey_Label");
    public static string SettingsAiApiKeyDescription => Get("Settings_AiApiKey_Description");
    public static string SettingsAutocompleteLabel => Get("Settings_Autocomplete_Label");
    public static string SettingsAutocompleteDescription => Get("Settings_Autocomplete_Description");
    public static string SettingsEmptySuggestionsLabel => Get("Settings_EmptySuggestions_Label");
    public static string SettingsEmptySuggestionsDescription => Get("Settings_EmptySuggestions_Description");
    public static string SettingsEmptySuggestionsNone => Get("Settings_EmptySuggestions_None");
    public static string SettingsEmptySuggestionsRecent => Get("Settings_EmptySuggestions_Recent");
    public static string SettingsEmptySuggestionsGoogleDefault => Get("Settings_EmptySuggestions_GoogleDefault");
    public static string SettingsEmptySuggestionsRecentAndGoogleDefault => Get("Settings_EmptySuggestions_RecentAndGoogleDefault");
    public static string SettingsFaviconsLabel => Get("Settings_Favicons_Label");
    public static string SettingsFaviconsDescription => Get("Settings_Favicons_Description");
    public static string SettingsGroupLocalResultsLabel => Get("Settings_GroupLocalResults_Label");
    public static string SettingsGroupLocalResultsDescription => Get("Settings_GroupLocalResults_Description");
    public static string SettingsDecodeDataImagesLabel => Get("Settings_DecodeDataImages_Label");
    public static string SettingsDecodeDataImagesDescription => Get("Settings_DecodeDataImages_Description");
    public static string SettingsMaxPerEngineLabel => Get("Settings_MaxPerEngine_Label");
    public static string SettingsMaxPerEngineDescription => Get("Settings_MaxPerEngine_Description");
    public static string SettingsMaxLocalLabel => Get("Settings_MaxLocal_Label");
    public static string SettingsMaxLocalDescription => Get("Settings_MaxLocal_Description");
    public static string SettingsMaxTotalLabel => Get("Settings_MaxTotal_Label");
    public static string SettingsMaxTotalDescription => Get("Settings_MaxTotal_Description");
    public static string SettingsDebounceLabel => Get("Settings_Debounce_Label");
    public static string SettingsDebounceDescription => Get("Settings_Debounce_Description");
    public static string SettingsAiAnswerDebugLabel => Get("Settings_AiAnswerDebug_Label");
    public static string SettingsAiAnswerDebugDescription => Get("Settings_AiAnswerDebug_Description");
    public static string RichDetailsAiDebugHeader => Get("RichDetails_AiDebug_Header");
    public static string RichDetailsAiDebugIdle => Get("RichDetails_AiDebug_Idle");
    public static string RichDetailsAiDebugDelaying => Get("RichDetails_AiDebug_Delaying");
    public static string RichDetailsAiDebugRequesting => Get("RichDetails_AiDebug_Requesting");
    public static string RichDetailsAiDebugStreaming => Get("RichDetails_AiDebug_Streaming");
    public static string RichDetailsAiDebugDone => Get("RichDetails_AiDebug_Done");
    public static string RichDetailsAiDebugCancelled => Get("RichDetails_AiDebug_Cancelled");
    public static string RichDetailsAiDebugDisabled => Get("RichDetails_AiDebug_Disabled");
    public static string RichDetailsAiDebugError(string message) => Format("RichDetails_AiDebug_Error", message);
    public static string RichDetailsAiDebugChunks(int count) => Format("RichDetails_AiDebug_Chunks", count);
    public static string RichDetailsAiDebugDuration(int milliseconds) => Format("RichDetails_AiDebug_Duration", milliseconds);
    public static string RichDetailsAiDebugEndpoint(string endpoint) => Format("RichDetails_AiDebug_Endpoint", endpoint);
    public static string RichDetailsAiDebugModel(string model) => Format("RichDetails_AiDebug_Model", model);
}
