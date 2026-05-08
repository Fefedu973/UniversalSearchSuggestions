using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions.Toolkit;
using UniversalSearchSuggestions.Core.Browsers;
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
    private const string AiAnswerEndpointKey = "ai_answer_endpoint";
    private const string AiAnswerModelKey = "ai_answer_model";
    private const string AutocompleteKey = "search_box_autocomplete";
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
        MigrateSettingsFile();
        LoadSettings();
        Settings.SettingsChanged += (_, _) => SaveSettings();
    }

    public SearchPreferences Snapshot()
    {
        var enableGoogle = ReadBool(GoogleKey, true);

        return new SearchPreferences
        {
            PrimaryEngine = ReadEngine(PrimaryEngineKey, SearchEngineKind.Google),
            BrowserId = ReadBrowserId(BrowserKey, "default"),
            LocalBrowserId = ReadLocalBrowserId(),
            CustomBrowserPath = ReadString(CustomBrowserPathKey, string.Empty),
            CustomSearchUrlTemplate = ReadString(CustomSearchUrlKey, "https://www.google.com/search?q={query}"),
            Language = ReadString(LanguageKey, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName),
            EnableGoogle = enableGoogle,
            EnableBing = ReadBool(BingKey, true),
            EnableYahoo = ReadBool(YahooKey, false),
            EnableDuckDuckGo = ReadBool(DuckDuckGoKey, true),
            EnableEcosia = ReadBool(EcosiaKey, false),
            EnableBrave = ReadBool(BraveKey, true),
            EnableQwant = ReadBool(QwantKey, false),
            EnableSwisscows = ReadBool(SwisscowsKey, false),
            EnableGoogleRichSuggestions = enableGoogle && ReadBool(RichGoogleKey, true),
            EnableGoogleOmniboxAnswers = enableGoogle && ReadBool(GoogleOmniboxAnswersKey, true),
            EnableGoogleToolbarSuggestions = enableGoogle && ReadBool(GoogleToolbarKey, false),
            IncludeBrowserBookmarks = ReadBool(BookmarksKey, true),
            IncludeBrowserHistory = ReadBool(HistoryKey, false),
            ShowDetails = ReadBool(DetailsKey, true),
            EnableRichWebDetails = ReadBool(RichWebDetailsKey, false),
            RichDetailsEndpointTemplate = ReadString(RichDetailsEndpointKey, string.Empty),
            EnableAiAnswerDetails = ReadBool(AiAnswerDetailsKey, false),
            RefreshListForLiveDetails = ReadBool(LiveDetailsRefreshKey, false),
            AiAnswerEndpointTemplate = ReadString(AiAnswerEndpointKey, "https://oai.endpoints.kepler.ai.cloud.ovh.net/v1/chat/completions"),
            AiAnswerModel = ReadString(AiAnswerModelKey, "Llama-3.1-8B-Instruct"),
            EnableSearchBoxAutocomplete = ReadBool(AutocompleteKey, true),
            ShowFavicons = ReadBool(FaviconsKey, true),
            GroupLocalBrowserResults = ReadBool(GroupLocalResultsKey, true),
            DecodeDataImages = ReadBool(DecodeDataImagesKey, true),
            MaxSuggestionsPerEngine = ReadInt(MaxPerEngineKey, 5, 1, 10),
            MaxLocalResults = ReadInt(MaxLocalKey, 5, 0, 12),
            MaxTotalResults = ReadInt(MaxTotalKey, 18, 5, 40),
            DebounceMilliseconds = ReadInt(DebounceKey, 110, 0, 500),
        };
    }

    private void RegisterSettings()
    {
        Settings.Add(new ChoiceSetSetting(
            PrimaryEngineKey,
            "Moteur d'ouverture",
            "Utilisé quand vous validez une suggestion de texte. Les sources Google, Bing, DuckDuckGo et Brave restent indépendantes.",
            [
                new ChoiceSetSetting.Choice("Google", SearchEngineKind.Google.ToString()),
                .. SearchEngineCatalog.BuiltInEngines
                    .Where(static engine => engine.Kind != SearchEngineKind.Google)
                    .Select(static engine => new ChoiceSetSetting.Choice(engine.DisplayName, engine.Kind.ToString())),
                new ChoiceSetSetting.Choice("URL personnalisée", SearchEngineKind.Custom.ToString()),
            ]));

        Settings.Add(new ChoiceSetSetting(
            BrowserKey,
            "Navigateur utilisé à l'ouverture",
            "Choisit seulement l'application qui ouvre l'URL. Le choix 'Navigateur par défaut' laisse Windows décider.",
            BuildOpeningBrowserChoices()));

        Settings.Add(new ChoiceSetSetting(
            LocalBrowserKey,
            "Navigateur pour favoris/historique",
            "Source locale scannée pour les favoris et l'historique. Un seul navigateur est lu pour garder la recherche rapide.",
            BuildLocalBrowserChoices()));

        Settings.Add(new TextSetting(
            CustomBrowserPathKey,
            "Chemin navigateur personnalisé",
            "Utilisé seulement si le navigateur d'ouverture est personnalisé.",
            string.Empty));

        Settings.Add(new TextSetting(
            CustomSearchUrlKey,
            "URL de recherche personnalisée",
            "Utilise {query}, {query+} ou %s comme emplacement de la requête.",
            "https://www.google.com/search?q={query}"));

        Settings.Add(new TextSetting(
            LanguageKey,
            "Langue/région des suggestions",
            "Code envoyé aux providers réseau pour orienter la langue et parfois le pays des suggestions. Utilisez fr-FR pour français/France, fr-CA pour français/Canada, en-US pour anglais/États-Unis.",
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName));

        Settings.Add(new ToggleSetting(GoogleKey, "Suggestions Google", "Utiliser l'API autocomplete Google.", true));
        Settings.Add(new ToggleSetting(BingKey, "Suggestions Bing", "Utiliser l'endpoint autocomplete Bing.", true));
        Settings.Add(new ToggleSetting(YahooKey, "Suggestions Yahoo", "Utiliser l'endpoint autocomplete Yahoo hérité de l'ancien plugin.", false));
        Settings.Add(new ToggleSetting(DuckDuckGoKey, "Suggestions DuckDuckGo", "Utiliser l'endpoint autocomplete DuckDuckGo.", true));
        Settings.Add(new ToggleSetting(EcosiaKey, "Suggestions Ecosia", "Utiliser l'endpoint autocomplete Ecosia hérité de l'ancien plugin.", false));
        Settings.Add(new ToggleSetting(BraveKey, "Suggestions Brave", "Utiliser l'endpoint autocomplete Brave.", true));
        Settings.Add(new ToggleSetting(QwantKey, "Suggestions Qwant", "Utiliser l'endpoint autocomplete Qwant hérité de l'ancien plugin.", false));
        Settings.Add(new ToggleSetting(SwisscowsKey, "Suggestions Swisscows", "Utiliser l'endpoint autocomplete Swisscows hérité de l'ancien plugin.", false));
        Settings.Add(new ToggleSetting(RichGoogleKey, "Suggestions Google enrichies", "Descriptions et miniatures quand Google les expose. Inactif si Suggestions Google est coupé.", true));
        Settings.Add(new ToggleSetting(GoogleOmniboxAnswersKey, "Réponses Google Omnibox", "Réponses inline exposées à Chrome par Google: calculatrice, faits courts et autres answers quand disponibles. Inactif si Suggestions Google est coupé.", true));
        Settings.Add(new ToggleSetting(GoogleToolbarKey, "Suggestions Google legacy", "Ancienne API XML Google Toolbar utilisée par le plugin PowerToys Run original. Désactivé par défaut pour éviter les doublons.", false));
        Settings.Add(new ToggleSetting(BookmarksKey, "Favoris du navigateur choisi", "Ajoute les favoris du navigateur local sélectionné ci-dessus.", true));
        Settings.Add(new ToggleSetting(HistoryKey, "Historique du navigateur choisi", "Ajoute l'historique local. Plus coûteux que les favoris; désactivé par défaut.", false));
        Settings.Add(new ToggleSetting(DetailsKey, "Panneau de détail", "Afficher le Markdown et les miniatures à droite.", true));
        Settings.Add(new ToggleSetting(RichWebDetailsKey, "Détails web enrichis (beta)", "Quand le panneau de détail est actif, charge en arrière-plan des résumés gratuits via DuckDuckGo Instant Answer et Wikipedia. N'affecte pas la vitesse des suggestions.", false));
        Settings.Add(new TextSetting(RichDetailsEndpointKey, "Endpoint SERP enrichi optionnel", "Optionnel. Pour OpenSERP/SearXNG auto-hébergé avec {query}, {query+} et {language}. Vide = DuckDuckGo Instant Answer puis Wikipedia.", string.Empty));
        Settings.Add(new ToggleSetting(AiAnswerDetailsKey, "Réponse IA dans les détails (beta)", "Quand le panneau de détail est actif, envoie la recherche à l'endpoint IA configuré ci-dessous et affiche la réponse Markdown progressivement. Désactivé par défaut.", false));
        Settings.Add(new ToggleSetting(LiveDetailsRefreshKey, "Forcer le live des détails", "Temporaire. Reconstruit la liste pendant le streaming pour mettre à jour le panneau Markdown. Cela peut réinitialiser la sélection et le scroll dans Command Palette.", false));
        Settings.Add(new TextSetting(AiAnswerEndpointKey, "Endpoint IA de détail", "Endpoint compatible OpenAI Chat Completions ou URL GET avec {prompt}. Par défaut: OVH AI Endpoints anonyme, sans clé mais très limité.", "https://oai.endpoints.kepler.ai.cloud.ovh.net/v1/chat/completions"));
        Settings.Add(new TextSetting(AiAnswerModelKey, "Modèle IA de détail", "Modèle envoyé à l'endpoint compatible OpenAI. Utilisé seulement par la réponse IA dans le panneau de détail.", "Llama-3.1-8B-Instruct"));
        Settings.Add(new ToggleSetting(AutocompleteKey, "Complétion dans le champ", "Autorise Command Palette à compléter le champ avec TextToSuggest via la touche flèche droite.", true));
        Settings.Add(new ToggleSetting(FaviconsKey, "Favicons des sites", "Affiche l'icône du site pour les URL, favoris, historique et suggestions de navigation.", true));
        Settings.Add(new ToggleSetting(GroupLocalResultsKey, "Séparer favoris/historique", "Affiche les résultats du navigateur local sous un séparateur dédié.", true));
        Settings.Add(new ToggleSetting(DecodeDataImagesKey, "Décoder les images base64", "Convertit les images data: en fichiers de cache locaux.", true));
        Settings.Add(new TextSetting(MaxPerEngineKey, "Max autocomplete par source", "Nombre de suggestions gardées pour chaque source réseau. Valeur autorisée: 1 à 10.", "5"));
        Settings.Add(new TextSetting(MaxLocalKey, "Max favoris/historique", "Nombre total de résultats locaux affichés. 0 coupe les résultats locaux sans désactiver les sources. Valeur autorisée: 0 à 12.", "5"));
        Settings.Add(new TextSetting(MaxTotalKey, "Max résultats affichés", "Nombre total de lignes affichées dans Command Palette après fusion. Valeur autorisée: 5 à 40.", "18"));
        Settings.Add(new TextSetting(DebounceKey, "Délai avant appels réseau (ms)", "Attend avant Google/Bing/etc. pour éviter les requêtes inutiles. L'action Rechercher apparaît immédiatement. Valeur autorisée: 0 à 500.", "110"));
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
            new("Même que le navigateur d'ouverture", "same"),
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
            changed |= AddDefaultValue(root, AutocompleteKey, true.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, FaviconsKey, true.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, GroupLocalResultsKey, true.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, GoogleOmniboxAnswersKey, true.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, YahooKey, false.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, EcosiaKey, false.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, QwantKey, false.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, SwisscowsKey, false.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, GoogleToolbarKey, false.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, RichWebDetailsKey, false.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, RichDetailsEndpointKey, string.Empty);
            changed |= AddDefaultValue(root, AiAnswerDetailsKey, false.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, LiveDetailsRefreshKey, false.ToString().ToLowerInvariant());
            changed |= AddDefaultValue(root, AiAnswerEndpointKey, "https://oai.endpoints.kepler.ai.cloud.ovh.net/v1/chat/completions");
            changed |= AddDefaultValue(root, AiAnswerModelKey, "Llama-3.1-8B-Instruct");

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
}
