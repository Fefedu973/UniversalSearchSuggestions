using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions.Toolkit;
using UniversalSearchSuggestions.Core.Browsers;
using UniversalSearchSuggestions.Core.Resources;
using UniversalSearchSuggestions.Core.Search;

namespace UniversalSearchSuggestions.Settings;

internal sealed class SearchSettingsManager : JsonSettingsManager
{
    private const string PrimaryEngineKey = "primary_engine";
    private const string BrowserKey = "browser";
    private const string LocalBrowserKey = "local_browser";
    private const string CustomBrowserPathKey = "custom_browser_path";
    private const string CustomSearchUrlKey = "custom_search_url";
    private const string LanguageKey = "language";
    private const string GoogleKey = "engine_google";
    private const string BingKey = "engine_bing";
    private const string YahooKey = "engine_yahoo";
    private const string DuckDuckGoKey = "engine_duckduckgo";
    private const string EcosiaKey = "engine_ecosia";
    private const string BraveKey = "engine_brave";
    private const string QwantKey = "engine_qwant";
    private const string SwisscowsKey = "engine_swisscows";
    private const string RichGoogleKey = "google_rich";
    private const string GoogleOmniboxAnswersKey = "google_omnibox_answers";
    private const string GoogleToolbarKey = "google_toolbar";
    private const string BookmarksKey = "local_bookmarks";
    private const string HistoryKey = "local_history";
    private const string DetailsKey = "details";
    private const string RichWebDetailsKey = "rich_web_details";
    private const string RichDetailsEndpointKey = "rich_details_endpoint";
    private const string AiAnswerDetailsKey = "ai_answer_details";
    private const string LiveDetailsRefreshKey = "live_details_refresh";
    private const string AiAnswerDebugKey = "ai_answer_debug";
    private const string AiAnswerEndpointKey = "ai_answer_endpoint";
    private const string AiAnswerModelKey = "ai_answer_model";
    private const string AiAnswerApiKeyKey = "ai_answer_api_key";
    private const string AutocompleteKey = "search_box_autocomplete";
    private const string EmptySuggestionsKey = "empty_suggestions";
    private const string FaviconsKey = "favicons";
    private const string GroupLocalResultsKey = "group_local_results";
    private const string DecodeDataImagesKey = "decode_data_images";
    private const string MaxPerEngineKey = "max_per_engine";
    private const string MaxLocalKey = "max_local";
    private const string MaxTotalKey = "max_total";
    private const string DebounceKey = "debounce_ms";

    public SearchSettingsManager()
    {
        FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UniversalSearchSuggestions",
            "settings.json");

        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        RegisterSettings();
        SeedFreshInstallDefaults();
        MigrateSettingsFile();
        LoadSettings();
        Settings.SettingsChanged += (_, _) => SaveSettings();
    }

    private void SeedFreshInstallDefaults()
    {
        if (File.Exists(FilePath))
        {
            return;
        }

        // ChoiceSetSetting uses its first choice as the default, but we want the
        // empty-state defaults to actually show suggestions on first launch.
        var root = new JsonObject
        {
            [EmptySuggestionsKey] = EmptySearchSuggestionsMode.RecentAndGoogleDefault.ToString(),
        };

        try
        {
            File.WriteAllText(FilePath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public SearchPreferences Snapshot()
    {
        var enableGoogle = ReadBool(GoogleKey, true);
        var defaultLanguage = DefaultLanguageTag();

        return new SearchPreferences
        {
            PrimaryEngine = ReadEngine(PrimaryEngineKey, SearchEngineKind.Google),
            BrowserId = ReadBrowserId(BrowserKey, "default"),
            LocalBrowserId = ReadLocalBrowserId(),
            CustomBrowserPath = ReadString(CustomBrowserPathKey, string.Empty),
            CustomSearchUrlTemplate = ReadString(CustomSearchUrlKey, "https://www.google.com/search?q={query}"),
            Language = ReadString(LanguageKey, defaultLanguage),
            EnableGoogle = enableGoogle,
            EnableBing = ReadBool(BingKey, false),
            EnableYahoo = ReadBool(YahooKey, false),
            EnableDuckDuckGo = ReadBool(DuckDuckGoKey, false),
            EnableEcosia = ReadBool(EcosiaKey, false),
            EnableBrave = ReadBool(BraveKey, false),
            EnableQwant = ReadBool(QwantKey, false),
            EnableSwisscows = ReadBool(SwisscowsKey, false),
            EnableGoogleRichSuggestions = enableGoogle && ReadBool(RichGoogleKey, true),
            EnableGoogleOmniboxAnswers = enableGoogle && ReadBool(GoogleOmniboxAnswersKey, true),
            EnableGoogleToolbarSuggestions = enableGoogle && ReadBool(GoogleToolbarKey, false),
            IncludeBrowserBookmarks = ReadBool(BookmarksKey, true),
            IncludeBrowserHistory = ReadBool(HistoryKey, true),
            ShowDetails = ReadBool(DetailsKey, true),
            EnableRichWebDetails = ReadBool(RichWebDetailsKey, true),
            RichDetailsEndpointTemplate = ReadString(RichDetailsEndpointKey, string.Empty),
            EnableAiAnswerDetails = ReadBool(AiAnswerDetailsKey, true),
            RefreshListForLiveDetails = ReadBool(LiveDetailsRefreshKey, true),
            EnableAiAnswerDebug = ReadBool(AiAnswerDebugKey, false),
            AiAnswerEndpointTemplate = ReadString(AiAnswerEndpointKey, "https://oai.endpoints.kepler.ai.cloud.ovh.net/v1/chat/completions"),
            AiAnswerModel = ReadString(AiAnswerModelKey, "Llama-3.1-8B-Instruct"),
            AiAnswerApiKey = ReadString(AiAnswerApiKeyKey, string.Empty),
            EnableSearchBoxAutocomplete = ReadBool(AutocompleteKey, true),
            EmptySearchSuggestionsMode = ReadEmptySuggestionsMode(),
            ShowFavicons = ReadBool(FaviconsKey, true),
            GroupLocalBrowserResults = ReadBool(GroupLocalResultsKey, true),
            DecodeDataImages = ReadBool(DecodeDataImagesKey, true),
            MaxSuggestionsPerEngine = ReadInt(MaxPerEngineKey, 10, 1, 10),
            MaxLocalResults = ReadInt(MaxLocalKey, 12, 0, 12),
            MaxTotalResults = ReadInt(MaxTotalKey, 40, 5, 40),
            DebounceMilliseconds = ReadInt(DebounceKey, 110, 0, 500),
        };
    }

    private void RegisterSettings()
    {
        Settings.Add(new ChoiceSetSetting(
            PrimaryEngineKey,
            Strings.SettingsPrimaryEngineLabel,
            Strings.SettingsPrimaryEngineDescription,
            [
                new ChoiceSetSetting.Choice("Google", SearchEngineKind.Google.ToString()),
                .. SearchEngineCatalog.BuiltInEngines
                    .Where(static engine => engine.Kind != SearchEngineKind.Google)
                    .Select(static engine => new ChoiceSetSetting.Choice(engine.DisplayName, engine.Kind.ToString())),
                new ChoiceSetSetting.Choice(Strings.SettingsCustomUrlChoice, SearchEngineKind.Custom.ToString()),
            ]));

        Settings.Add(new ChoiceSetSetting(
            BrowserKey,
            Strings.SettingsOpeningBrowserLabel,
            Strings.SettingsOpeningBrowserDescription,
            BuildOpeningBrowserChoices()));

        Settings.Add(new ChoiceSetSetting(
            LocalBrowserKey,
            Strings.SettingsLocalBrowserLabel,
            Strings.SettingsLocalBrowserDescription,
            BuildLocalBrowserChoices()));

        Settings.Add(new TextSetting(
            CustomBrowserPathKey,
            Strings.SettingsCustomBrowserPathLabel,
            Strings.SettingsCustomBrowserPathDescription,
            string.Empty));

        Settings.Add(new TextSetting(
            CustomSearchUrlKey,
            Strings.SettingsCustomSearchUrlLabel,
            Strings.SettingsCustomSearchUrlDescription,
            "https://www.google.com/search?q={query}"));

        Settings.Add(new TextSetting(
            LanguageKey,
            Strings.SettingsLanguageLabel,
            Strings.SettingsLanguageDescription,
            DefaultLanguageTag()));

        Settings.Add(new ToggleSetting(GoogleKey, Strings.SettingsGoogleLabel, Strings.SettingsGoogleDescription, true));
        Settings.Add(new ToggleSetting(BingKey, Strings.SettingsBingLabel, Strings.SettingsBingDescription, false));
        Settings.Add(new ToggleSetting(YahooKey, Strings.SettingsYahooLabel, Strings.SettingsYahooDescription, false));
        Settings.Add(new ToggleSetting(DuckDuckGoKey, Strings.SettingsDuckDuckGoLabel, Strings.SettingsDuckDuckGoDescription, false));
        Settings.Add(new ToggleSetting(EcosiaKey, Strings.SettingsEcosiaLabel, Strings.SettingsEcosiaDescription, false));
        Settings.Add(new ToggleSetting(BraveKey, Strings.SettingsBraveLabel, Strings.SettingsBraveDescription, false));
        Settings.Add(new ToggleSetting(QwantKey, Strings.SettingsQwantLabel, Strings.SettingsQwantDescription, false));
        Settings.Add(new ToggleSetting(SwisscowsKey, Strings.SettingsSwisscowsLabel, Strings.SettingsSwisscowsDescription, false));
        Settings.Add(new ToggleSetting(RichGoogleKey, Strings.SettingsGoogleRichLabel, Strings.SettingsGoogleRichDescription, true));
        Settings.Add(new ToggleSetting(GoogleOmniboxAnswersKey, Strings.SettingsGoogleOmniboxLabel, Strings.SettingsGoogleOmniboxDescription, true));
        Settings.Add(new ToggleSetting(GoogleToolbarKey, Strings.SettingsGoogleToolbarLabel, Strings.SettingsGoogleToolbarDescription, false));
        Settings.Add(new ToggleSetting(BookmarksKey, Strings.SettingsBookmarksLabel, Strings.SettingsBookmarksDescription, true));
        Settings.Add(new ToggleSetting(HistoryKey, Strings.SettingsHistoryLabel, Strings.SettingsHistoryDescription, true));
        Settings.Add(new ToggleSetting(DetailsKey, Strings.SettingsDetailsLabel, Strings.SettingsDetailsDescription, true));
        Settings.Add(new ToggleSetting(RichWebDetailsKey, Strings.SettingsRichWebDetailsLabel, Strings.SettingsRichWebDetailsDescription, true));
        Settings.Add(new TextSetting(RichDetailsEndpointKey, Strings.SettingsRichDetailsEndpointLabel, Strings.SettingsRichDetailsEndpointDescription, string.Empty));
        Settings.Add(new ToggleSetting(AiAnswerDetailsKey, Strings.SettingsAiAnswerLabel, Strings.SettingsAiAnswerDescription, true));
        Settings.Add(new ToggleSetting(LiveDetailsRefreshKey, Strings.SettingsLiveDetailsRefreshLabel, Strings.SettingsLiveDetailsRefreshDescription, true));
        Settings.Add(new TextSetting(AiAnswerEndpointKey, Strings.SettingsAiEndpointLabel, Strings.SettingsAiEndpointDescription, "https://oai.endpoints.kepler.ai.cloud.ovh.net/v1/chat/completions"));
        Settings.Add(new TextSetting(AiAnswerModelKey, Strings.SettingsAiModelLabel, Strings.SettingsAiModelDescription, "Llama-3.1-8B-Instruct"));
        Settings.Add(new TextSetting(AiAnswerApiKeyKey, Strings.SettingsAiApiKeyLabel, Strings.SettingsAiApiKeyDescription, string.Empty));
        Settings.Add(new ToggleSetting(AutocompleteKey, Strings.SettingsAutocompleteLabel, Strings.SettingsAutocompleteDescription, true));
        Settings.Add(new ChoiceSetSetting(
            EmptySuggestionsKey,
            Strings.SettingsEmptySuggestionsLabel,
            Strings.SettingsEmptySuggestionsDescription,
            [
                new ChoiceSetSetting.Choice(Strings.SettingsEmptySuggestionsNone, EmptySearchSuggestionsMode.None.ToString()),
                new ChoiceSetSetting.Choice(Strings.SettingsEmptySuggestionsRecent, EmptySearchSuggestionsMode.RecentSearches.ToString()),
                new ChoiceSetSetting.Choice(Strings.SettingsEmptySuggestionsGoogleDefault, EmptySearchSuggestionsMode.GoogleDefault.ToString()),
                new ChoiceSetSetting.Choice(Strings.SettingsEmptySuggestionsRecentAndGoogleDefault, EmptySearchSuggestionsMode.RecentAndGoogleDefault.ToString()),
            ]));
        Settings.Add(new ToggleSetting(FaviconsKey, Strings.SettingsFaviconsLabel, Strings.SettingsFaviconsDescription, true));
        Settings.Add(new ToggleSetting(GroupLocalResultsKey, Strings.SettingsGroupLocalResultsLabel, Strings.SettingsGroupLocalResultsDescription, true));
        Settings.Add(new ToggleSetting(DecodeDataImagesKey, Strings.SettingsDecodeDataImagesLabel, Strings.SettingsDecodeDataImagesDescription, true));
        Settings.Add(new TextSetting(MaxPerEngineKey, Strings.SettingsMaxPerEngineLabel, Strings.SettingsMaxPerEngineDescription, "10"));
        Settings.Add(new TextSetting(MaxLocalKey, Strings.SettingsMaxLocalLabel, Strings.SettingsMaxLocalDescription, "12"));
        Settings.Add(new TextSetting(MaxTotalKey, Strings.SettingsMaxTotalLabel, Strings.SettingsMaxTotalDescription, "40"));
        Settings.Add(new TextSetting(DebounceKey, Strings.SettingsDebounceLabel, Strings.SettingsDebounceDescription, "110"));
        Settings.Add(new ToggleSetting(AiAnswerDebugKey, Strings.SettingsAiAnswerDebugLabel, Strings.SettingsAiAnswerDebugDescription, false));
    }

    private bool ReadBool(string key, bool fallback)
    {
        if (Settings.TryGetSetting<bool>(key, out var value))
        {
            return value;
        }

        return Settings.TryGetSetting<string>(key, out var text) && bool.TryParse(text, out var parsed)
            ? parsed
            : fallback;
    }

    private string ReadString(string key, string fallback)
    {
        return Settings.TryGetSetting<string>(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private int ReadInt(string key, int fallback, int min, int max)
    {
        if (Settings.TryGetSetting<int>(key, out var integer))
        {
            return Math.Clamp(integer, min, max);
        }

        var text = ReadString(key, fallback.ToString(CultureInfo.InvariantCulture));
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? Math.Clamp(value, min, max)
            : fallback;
    }

    private SearchEngineKind ReadEngine(string key, SearchEngineKind fallback)
    {
        var text = ReadString(key, fallback.ToString());
        return SearchEngineCatalog.TryParseEngine(text, out var engine) ? engine : fallback;
    }

    private EmptySearchSuggestionsMode ReadEmptySuggestionsMode()
    {
        var text = ReadString(EmptySuggestionsKey, EmptySearchSuggestionsMode.RecentAndGoogleDefault.ToString());
        if (text.Equals("GoogleTrending", StringComparison.OrdinalIgnoreCase))
        {
            return EmptySearchSuggestionsMode.GoogleDefault;
        }

        if (text.Equals("RecentAndGoogleTrending", StringComparison.OrdinalIgnoreCase))
        {
            return EmptySearchSuggestionsMode.RecentAndGoogleDefault;
        }

        return Enum.TryParse<EmptySearchSuggestionsMode>(text, ignoreCase: true, out var mode)
            ? mode
            : EmptySearchSuggestionsMode.RecentAndGoogleDefault;
    }

    private string ReadBrowserId(string key, string fallback)
    {
        var value = ReadString(key, fallback);
        var browser = BrowserInstallDetector.DetectInstalledBrowsers()
            .FirstOrDefault(browser =>
                browser.Id.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                browser.DisplayName.Equals(value, StringComparison.OrdinalIgnoreCase));

        return browser?.Id ?? fallback;
    }

    private string ReadLocalBrowserId()
    {
        var value = ReadString(LocalBrowserKey, "same");
        if (value.Equals("same", StringComparison.OrdinalIgnoreCase))
        {
            return "same";
        }

        return ReadBrowserId(LocalBrowserKey, "same");
    }

    private static List<ChoiceSetSetting.Choice> BuildOpeningBrowserChoices()
    {
        return BrowserInstallDetector.DetectInstalledBrowsers()
            .Select(static browser => new ChoiceSetSetting.Choice(browser.DisplayName, browser.Id))
            .ToList();
    }

    private static List<ChoiceSetSetting.Choice> BuildLocalBrowserChoices()
    {
        var choices = new List<ChoiceSetSetting.Choice>
        {
            new(Strings.SettingsLocalBrowserSameAsOpening, "same"),
        };

        choices.AddRange(BrowserInstallDetector.DetectInstalledBrowsers()
            .Where(static browser => browser.Kind is not (BrowserKind.Default or BrowserKind.Custom))
            .Select(static browser => new ChoiceSetSetting.Choice(browser.DisplayName, browser.Id)));

        return choices;
    }

    private void MigrateSettingsFile()
    {
        if (!File.Exists(FilePath))
        {
            return;
        }

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(FilePath)) as JsonObject;
            if (root is null)
            {
                return;
            }

            var isPreSettingsRefactor =
                !root.ContainsKey(LocalBrowserKey) &&
                !root.ContainsKey(AutocompleteKey) &&
                !root.ContainsKey(FaviconsKey) &&
                !root.ContainsKey(GroupLocalResultsKey);

            var changed = NormalizeBrowserChoice(root, BrowserKey);
            changed |= NormalizeBrowserChoice(root, LocalBrowserKey);
            changed |= AddDefaultValue(root, LocalBrowserKey, "same");
            changed |= AddDefaultValue(root, LanguageKey, DefaultLanguageTag());
            changed |= AddDefaultValue(root, BingKey, false.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, DuckDuckGoKey, false.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, BraveKey, false.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, AutocompleteKey, true.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, FaviconsKey, true.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, GroupLocalResultsKey, true.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, GoogleOmniboxAnswersKey, true.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, YahooKey, false.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, EcosiaKey, false.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, QwantKey, false.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, SwisscowsKey, false.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, GoogleToolbarKey, false.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, BookmarksKey, true.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, HistoryKey, true.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, RichWebDetailsKey, true.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, RichDetailsEndpointKey, string.Empty);
            changed |= AddDefaultValue(root, AiAnswerDetailsKey, true.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, LiveDetailsRefreshKey, true.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, AiAnswerDebugKey, false.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, AiAnswerEndpointKey, "https://oai.endpoints.kepler.ai.cloud.ovh.net/v1/chat/completions");
            changed |= AddDefaultValue(root, AiAnswerModelKey, "Llama-3.1-8B-Instruct");
            changed |= AddDefaultValue(root, AiAnswerApiKeyKey, string.Empty);
            changed |= AddDefaultValue(root, EmptySuggestionsKey, EmptySearchSuggestionsMode.RecentAndGoogleDefault.ToString());

            if (root.TryGetPropertyValue(AiAnswerEndpointKey, out var aiEndpointNode) &&
                aiEndpointNode?.GetValue<string>().Equals("https://text.pollinations.ai/{prompt}", StringComparison.OrdinalIgnoreCase) == true)
            {
                root[AiAnswerEndpointKey] = "https://oai.endpoints.kepler.ai.cloud.ovh.net/v1/chat/completions";
                changed = true;
            }

            if (root.TryGetPropertyValue(AiAnswerModelKey, out var aiModelNode) &&
                aiModelNode?.GetValue<string>().Equals("Meta-Llama-3_1-8B-Instruct", StringComparison.OrdinalIgnoreCase) == true)
            {
                root[AiAnswerModelKey] = "Llama-3.1-8B-Instruct";
                changed = true;
            }

            if (isPreSettingsRefactor &&
                root.TryGetPropertyValue(PrimaryEngineKey, out var primaryEngineNode) &&
                primaryEngineNode?.GetValue<string>().Equals(SearchEngineKind.Bing.ToString(), StringComparison.OrdinalIgnoreCase) == true)
            {
                root[PrimaryEngineKey] = SearchEngineKind.Google.ToString();
                changed = true;
            }

            if (changed)
            {
                File.WriteAllText(FilePath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool NormalizeBrowserChoice(JsonObject root, string key)
    {
        if (!root.TryGetPropertyValue(key, out var valueNode))
        {
            return false;
        }

        var value = valueNode?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(value) || value.Equals("same", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var browser = BrowserInstallDetector.DetectInstalledBrowsers()
            .FirstOrDefault(browser =>
                browser.Id.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                browser.DisplayName.Equals(value, StringComparison.OrdinalIgnoreCase));

        if (browser is null || browser.Id.Equals(value, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        root[key] = browser.Id;
        return true;
    }

    private static bool AddDefaultValue(JsonObject root, string key, string value)
    {
        if (root.ContainsKey(key))
        {
            return false;
        }

        root[key] = value;
        return true;
    }

    private static string DefaultLanguageTag()
    {
        var culture = CultureInfo.CurrentCulture.Name;
        if (!string.IsNullOrWhiteSpace(culture))
        {
            return culture;
        }

        culture = CultureInfo.CurrentUICulture.Name;
        return string.IsNullOrWhiteSpace(culture) ? "en-US" : culture;
    }
}
