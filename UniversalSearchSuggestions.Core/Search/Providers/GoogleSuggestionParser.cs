using System.Text.Json;
using System.Xml.Linq;
using UniversalSearchSuggestions.Core.Resources;
using UniversalSearchSuggestions.Core.Utilities;

namespace UniversalSearchSuggestions.Core.Search.Providers;

public static class GoogleSuggestionParser
{
    private enum GoogleOmniboxAnswerType
    {
        Unspecified = 0,
        Dictionary = 1,
        Finance = 2,
        GenericAnswer = 3,
        Local = 4,
        Sports = 5,
        SunriseSunset = 6,
        Translation = 7,
        Weather = 8,
        WhenIs = 9,
        Currency = 10,
        LocalTime = 11,
        PlayInstall = 12,
    }

    private sealed record OmniboxAnswer(GoogleOmniboxAnswerType Type, string? Summary, string? ImageUrl);

    private sealed record ParsedSuggestRoot(
        JsonDocument Document,
        JsonElement Suggestions,
        JsonElement Descriptions,
        JsonElement Metadata,
        JsonElement Types,
        JsonElement Details,
        JsonElement Relevances,
        JsonElement Subtypes) : IDisposable
    {
        public void Dispose() => Document.Dispose();
    }

    public static IReadOnlyList<SearchSuggestion> ParseRichSuggestions(string payload, string query)
    {
        return ParseGwsRows(
            payload,
            fallbackSection: SearchEngineCatalog.Google.SuggestionSection,
            scoreBase: 70,
            targetUriFactory: static cleanText => SearchEngineCatalog.BuildSearchUri(SearchEngineKind.Google, cleanText));
    }

    public static IReadOnlyList<SearchSuggestion> ParseDefaultSuggestions(string payload, SearchPreferences preferences)
    {
        return ParseGwsRows(
            payload,
            fallbackSection: Strings.SectionGoogleDefaultSuggestions,
            scoreBase: 120,
            targetUriFactory: query => SuggestionFetchService.BuildPreferredSearchUri(query, preferences));
    }

    private static List<SearchSuggestion> ParseGwsRows(
        string payload,
        string fallbackSection,
        int scoreBase,
        Func<string, Uri> targetUriFactory)
    {
        var json = StripGoogleCallback(payload);
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Array ||
            document.RootElement.GetArrayLength() == 0 ||
            document.RootElement[0].ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<SearchSuggestion>();
        foreach (var row in document.RootElement[0].EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() == 0)
            {
                continue;
            }

            var rawText = row[0].GetString() ?? string.Empty;
            var cleanText = TextSanitizer.NormalizeWhitespace(TextSanitizer.FromSuggestionHtml(rawText));
            if (string.IsNullOrWhiteSpace(cleanText))
            {
                continue;
            }

            string? detailTitle = null;
            string? description = null;
            string? imageUrl = null;
            if (row.GetArrayLength() > 3 && row[3].ValueKind == JsonValueKind.Object)
            {
                var detail = row[3];
                detailTitle = CleanOmniboxText(TryGetString(detail, "zh"));
                description = CleanOmniboxText(TryGetString(detail, "zi"));
                imageUrl = TryGetString(detail, "zs");
            }

            var title = string.IsNullOrWhiteSpace(detailTitle) ? cleanText : detailTitle!;
            results.Add(new SearchSuggestion
            {
                Title = title,
                Query = cleanText,
                TargetUri = targetUriFactory(cleanText),
                Engine = SearchEngineKind.Google,
                SourceKind = SuggestionSourceKind.SearchEngine,
                Description = description,
                ImageUrl = imageUrl,
                Section = fallbackSection,
                TextToSuggest = cleanText,
                Score = scoreBase - results.Count,
            });
        }

        return results;
    }

    public static IReadOnlyList<SearchSuggestion> ParseChromeSuggestions(string payload, string query)
    {
        using var root = TryParseSuggestRoot(payload, query);
        if (root is null)
        {
            return [];
        }

        var results = new List<SearchSuggestion>();
        var index = 0;

        foreach (var item in root.Suggestions.EnumerateArray())
        {
            var text = CleanOmniboxText(item.GetString());
            if (string.IsNullOrWhiteSpace(text))
            {
                index++;
                continue;
            }

            var description = CleanOmniboxText(GetStringAt(root.Descriptions, index));
            var suggestType = NormalizeSuggestType(GetStringAt(root.Types, index));
            var detail = GetElementAt(root.Details, index);
            var answer = ExtractOmniboxAnswer(detail, query, text);
            var displayText = ResolveDisplayText(detail, query, text, suggestType);
            var fillIntoEdit = ResolveFillIntoEdit(text, suggestType);
            var isNavigation = suggestType.Equals("NAVIGATION", StringComparison.OrdinalIgnoreCase) ||
                UrlInputParser.TryParse(text, out _);

            var target = isNavigation && UrlInputParser.TryParse(text, out var parsedUri)
                ? parsedUri
                : SearchEngineCatalog.BuildSearchUri(SearchEngineKind.Google, fillIntoEdit);

            results.Add(new SearchSuggestion
            {
                Title = displayText,
                Query = fillIntoEdit,
                TargetUri = target,
                Engine = SearchEngineKind.Google,
                SourceKind = SuggestionSourceKind.SearchEngine,
                Description = SelectDescription(answer.Summary, description, fallback: string.Empty),
                ImageUrl = answer.ImageUrl,
                Section = SearchEngineCatalog.Google.SuggestionSection,
                TextToSuggest = fillIntoEdit,
                IsNavigation = isNavigation,
                IconHint = ResolveOmniboxIconHint(suggestType, answer.Type, description, defaultHint: string.Empty),
                Score = ResolveSuggestionScore(suggestType, answer.Type, GetIntAt(root.Relevances, index, 0), index),
            });

            index++;
        }

        return results;
    }

    public static IReadOnlyList<SearchSuggestion> ParseChromeOmniboxAnswerSuggestions(string payload, string query)
    {
        using var root = TryParseSuggestRoot(payload, query);
        if (root is null)
        {
            return [];
        }

        var results = new List<SearchSuggestion>();
        var index = 0;

        foreach (var item in root.Suggestions.EnumerateArray())
        {
            var text = CleanOmniboxText(item.GetString());
            if (string.IsNullOrWhiteSpace(text))
            {
                index++;
                continue;
            }

            var description = CleanOmniboxText(GetStringAt(root.Descriptions, index));
            var suggestType = NormalizeSuggestType(GetStringAt(root.Types, index));
            var isCalculator = suggestType.Equals("CALCULATOR", StringComparison.OrdinalIgnoreCase);
            var detail = GetElementAt(root.Details, index);
            var answer = ExtractOmniboxAnswer(detail, query, text);
            if (!isCalculator && answer.Type == GoogleOmniboxAnswerType.Unspecified && string.IsNullOrWhiteSpace(answer.Summary))
            {
                index++;
                continue;
            }

            var displayText = ResolveDisplayText(detail, query, text, suggestType);
            var fillIntoEdit = ResolveFillIntoEdit(text, suggestType);
            var exactInputAnswer = TextEquals(text, query) && (!string.IsNullOrWhiteSpace(answer.Summary) || answer.Type != GoogleOmniboxAnswerType.Unspecified);
            var resultQuery = exactInputAnswer ? query : text;
            var targetQuery = isCalculator || exactInputAnswer ? query : fillIntoEdit;
            var resultDescription = isCalculator
                ? SelectDescription(null, description, Strings.AnswerTypeCalculator)
                : SelectDescription(answer.Summary, description, GetAnswerTypeLabel(answer.Type));

            results.Add(new SearchSuggestion
            {
                Title = displayText,
                Query = resultQuery,
                TargetUri = SearchEngineCatalog.BuildSearchUri(SearchEngineKind.Google, targetQuery),
                Engine = SearchEngineKind.Google,
                SourceKind = SuggestionSourceKind.SearchAnswer,
                Description = resultDescription,
                ImageUrl = answer.ImageUrl,
                Section = Strings.SectionGoogleAnswers,
                TextToSuggest = fillIntoEdit,
                IconHint = ResolveOmniboxIconHint(suggestType, answer.Type, resultDescription, defaultHint: "answer"),
                Score = ResolveOmniboxAnswerScore(suggestType, answer.Type, GetIntAt(root.Relevances, index, 0), index),
            });

            index++;
        }

        return results;
    }

    public static IReadOnlyList<SearchSuggestion> ParseToolbarSuggestions(string payload)
    {
        var document = XDocument.Parse(payload);
        var results = new List<SearchSuggestion>();

        foreach (var value in document.Descendants("suggestion")
            .Select(static element => element.Attribute("data")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            var clean = TextSanitizer.NormalizeWhitespace(value!);
            if (string.IsNullOrWhiteSpace(clean))
            {
                continue;
            }

            results.Add(new SearchSuggestion
            {
                Title = clean,
                Query = clean,
                TargetUri = SearchEngineCatalog.BuildSearchUri(SearchEngineKind.Google, clean),
                Engine = SearchEngineKind.Google,
                SourceKind = SuggestionSourceKind.SearchEngine,
                Section = SearchEngineCatalog.Google.SuggestionSection,
                TextToSuggest = clean,
                Score = 42 - results.Count,
            });
        }

        return results;
    }

    private static string StripGoogleCallback(string payload)
    {
        var trimmed = payload.Trim();
        const string prefix = "window.google.ac.h(";
        if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
        {
            trimmed = trimmed[prefix.Length..];
            if (trimmed.EndsWith(')'))
            {
                trimmed = trimmed[..^1];
            }
        }

        return StripXssiGuard(trimmed);
    }

    private static string StripXssiGuard(string payload)
    {
        var trimmed = payload.TrimStart();
        var start = trimmed.IndexOf('[', StringComparison.Ordinal);
        return start > 0 ? trimmed[start..] : trimmed;
    }

    private static ParsedSuggestRoot? TryParseSuggestRoot(string payload, string query)
    {
        var document = ParseSuggestDocument(payload);
        if (document.RootElement.ValueKind != JsonValueKind.Array ||
            document.RootElement.GetArrayLength() < 2 ||
            document.RootElement[0].ValueKind != JsonValueKind.String ||
            !TextEquals(document.RootElement[0].GetString() ?? string.Empty, query) ||
            document.RootElement[1].ValueKind != JsonValueKind.Array)
        {
            document.Dispose();
            return null;
        }

        var suggestions = document.RootElement[1];
        var descriptions = document.RootElement.GetArrayLength() > 2 && document.RootElement[2].ValueKind == JsonValueKind.Array
            ? document.RootElement[2]
            : default;

        var metadata = document.RootElement.GetArrayLength() > 4 && document.RootElement[4].ValueKind == JsonValueKind.Object
            ? document.RootElement[4]
            : default;

        var types = TryGetArray(metadata, "google:suggesttype");
        var details = TryGetSizedArray(metadata, "google:suggestdetail", suggestions.GetArrayLength());
        var relevances = TryGetSizedArray(metadata, "google:suggestrelevance", suggestions.GetArrayLength());
        var subtypes = TryGetSizedArray(metadata, "google:suggestsubtypes", suggestions.GetArrayLength());
        return new ParsedSuggestRoot(document, suggestions, descriptions, metadata, types, details, relevances, subtypes);
    }

    private static JsonDocument ParseSuggestDocument(string payload)
    {
        var remaining = payload.TrimStart();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var start = remaining.IndexOf('[', StringComparison.Ordinal);
            if (start < 0)
            {
                break;
            }

            var candidate = remaining[start..];
            try
            {
                return JsonDocument.Parse(candidate);
            }
            catch (JsonException) when (candidate.Length > 1)
            {
                remaining = candidate[1..];
            }
        }

        return JsonDocument.Parse(StripXssiGuard(payload));
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static JsonElement TryGetArray(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Array
                ? property
                : default;
    }

    private static JsonElement TryGetSizedArray(JsonElement element, string propertyName, int expectedLength)
    {
        var array = TryGetArray(element, propertyName);
        return array.ValueKind == JsonValueKind.Array && array.GetArrayLength() == expectedLength
            ? array
            : default;
    }

    private static string GetStringAt(JsonElement array, int index)
    {
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() <= index)
        {
            return string.Empty;
        }

        return array[index].ValueKind == JsonValueKind.String
            ? array[index].GetString() ?? string.Empty
            : string.Empty;
    }

    private static int GetIntAt(JsonElement array, int index, int fallback)
    {
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() <= index)
        {
            return fallback;
        }

        return TryReadInt(array[index]) ?? fallback;
    }

    private static JsonElement GetElementAt(JsonElement array, int index)
    {
        return array.ValueKind == JsonValueKind.Array && array.GetArrayLength() > index ? array[index] : default;
    }

    private static string NormalizeSuggestType(string suggestType)
    {
        return suggestType.ToUpperInvariant() switch
        {
            "QUERY" => "QUERY",
            "CALCULATOR" => "CALCULATOR",
            "ENTITY" => "ENTITY",
            "TAIL" => "TAIL",
            "PERSONALIZED_QUERY" => "PERSONALIZED_QUERY",
            "PROFILE" => "PROFILE",
            "NAVIGATION" => "NAVIGATION",
            "PERSONALIZED_NAVIGATION" => "PERSONALIZED_NAVIGATION",
            "CHROME_QUERY_TILES" => "CHROME_QUERY_TILES",
            "CATEGORICAL_QUERY" => "CATEGORICAL_QUERY",
            "FUSEBOX_ACTION" => "FUSEBOX_ACTION",
            _ => "QUERY",
        };
    }

    private static string ResolveFillIntoEdit(string suggestion, string suggestType)
    {
        if (suggestType.Equals("CALCULATOR", StringComparison.OrdinalIgnoreCase) &&
            suggestion.StartsWith("= ", StringComparison.Ordinal))
        {
            return CleanOmniboxText(suggestion[2..]);
        }

        return suggestion;
    }

    private static string ResolveDisplayText(JsonElement detail, string query, string suggestion, string suggestType)
    {
        if (detail.ValueKind == JsonValueKind.Object &&
            detail.TryGetProperty("t", out var detailText) &&
            detailText.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(detailText.GetString()))
        {
            return CleanOmniboxText(detailText.GetString());
        }

        if (suggestType.Equals("CALCULATOR", StringComparison.OrdinalIgnoreCase))
        {
            var fillIntoEdit = ResolveFillIntoEdit(suggestion, suggestType);
            return $"{query} = {fillIntoEdit}";
        }

        return suggestion;
    }

    private static string SelectDescription(string? answerDescription, string description, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(answerDescription))
        {
            return answerDescription!;
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        return fallback;
    }

    private static OmniboxAnswer ExtractOmniboxAnswer(JsonElement detail, string query, string suggestion)
    {
        if (detail.ValueKind != JsonValueKind.Object)
        {
            return new OmniboxAnswer(GoogleOmniboxAnswerType.Unspecified, null, null);
        }

        var answerType = ExtractAnswerType(detail);
        var values = new List<string>();
        if (detail.TryGetProperty("ansa", out var answer) &&
            answer.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            CollectAnswerText(answer, values);
        }

        if (values.Count == 0 &&
            detail.TryGetProperty("t", out var tailText) &&
            tailText.ValueKind == JsonValueKind.String)
        {
            values.Add(tailText.GetString() ?? string.Empty);
        }

        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            TextSanitizer.NormalizeWhitespace(query),
            TextSanitizer.NormalizeWhitespace(suggestion),
        };

        var meaningfulValues = values
            .Select(CleanOmniboxText)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Where(value => !ignored.Contains(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();

        var imageUrl = TryGetString(detail, "i") ??
            TryGetString(detail, "image") ??
            TryGetString(detail, "zs");

        return new OmniboxAnswer(
            answerType,
            meaningfulValues.Length == 0 ? null : string.Join(" - ", meaningfulValues),
            NormalizeImageUrl(imageUrl));
    }

    private static GoogleOmniboxAnswerType ExtractAnswerType(JsonElement detail)
    {
        if (detail.ValueKind != JsonValueKind.Object ||
            !detail.TryGetProperty("ansb", out var answerTypeElement))
        {
            return GoogleOmniboxAnswerType.Unspecified;
        }

        var value = TryReadInt(answerTypeElement);
        return value is >= 0 and <= 12
            ? (GoogleOmniboxAnswerType)value.Value
            : GoogleOmniboxAnswerType.Unspecified;
    }

    private static int? TryReadInt(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt32(out var value) => value,
            JsonValueKind.String when int.TryParse(element.GetString(), out var value) => value,
            _ => null,
        };
    }

    private static string? ExtractAnswerSummary(JsonElement detail, string query, string suggestion)
    {
        return ExtractOmniboxAnswer(detail, query, suggestion).Summary;
    }

    private static string? NormalizeImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        if (imageUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return $"https:{imageUrl}";
        }

        return Uri.TryCreate(imageUrl, UriKind.Absolute, out _) ? imageUrl : null;
    }

    private static void CollectAnswerText(JsonElement element, List<string> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (ShouldIgnoreAnswerProperty(property.Name))
                    {
                        continue;
                    }

                    if (property.Value.ValueKind == JsonValueKind.String &&
                        IsHumanReadableAnswerValue(property.Name, property.Value.GetString()))
                    {
                        values.Add(property.Value.GetString() ?? string.Empty);
                    }
                    else
                    {
                        CollectAnswerText(property.Value, values);
                    }
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectAnswerText(item, values);
                }

                break;
        }
    }

    private static bool ShouldIgnoreAnswerProperty(string propertyName)
    {
        return propertyName.StartsWith("google:", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Equals("du", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Equals("zs", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Equals("zl", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Equals("tt", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHumanReadableAnswerValue(string propertyName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out _))
        {
            return false;
        }

        if (trimmed.Length > 160 &&
            trimmed.All(static ch => char.IsLetterOrDigit(ch) || ch is '+' or '/' or '='))
        {
            return false;
        }

        return propertyName.Equals("t", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Equals("text", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Equals("value", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Equals("title", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Equals("subtitle", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Equals("label", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Equals("name", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Length <= 3;
    }

    private static bool TextEquals(string left, string right)
    {
        return string.Equals(
            TextSanitizer.NormalizeWhitespace(left),
            TextSanitizer.NormalizeWhitespace(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveOmniboxIconHint(
        string suggestType,
        GoogleOmniboxAnswerType answerType,
        string? description,
        string defaultHint)
    {
        if (suggestType.Equals("CALCULATOR", StringComparison.OrdinalIgnoreCase))
        {
            return "calculator";
        }

        return answerType switch
        {
            GoogleOmniboxAnswerType.Dictionary => "dictionary",
            GoogleOmniboxAnswerType.Finance => "finance",
            GoogleOmniboxAnswerType.Sports => "sports",
            GoogleOmniboxAnswerType.SunriseSunset => "weather",
            GoogleOmniboxAnswerType.Translation => "translate",
            GoogleOmniboxAnswerType.Weather => "weather",
            GoogleOmniboxAnswerType.Currency => "currency",
            GoogleOmniboxAnswerType.LocalTime => "time",
            GoogleOmniboxAnswerType.Local => "local",
            GoogleOmniboxAnswerType.WhenIs => "time",
            GoogleOmniboxAnswerType.PlayInstall => "app",
            GoogleOmniboxAnswerType.GenericAnswer => "answer",
            _ => ResolveOmniboxIconHintFromText(suggestType, description, defaultHint),
        };
    }

    private static string ResolveOmniboxIconHintFromText(string suggestType, string? description, string defaultHint)
    {
        var text = $"{suggestType} {description}";
        if (text.Contains("TRANSLAT", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("traduction", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("translate", StringComparison.OrdinalIgnoreCase))
        {
            return "translate";
        }

        return defaultHint;
    }

    private static string GetAnswerTypeLabel(GoogleOmniboxAnswerType answerType)
    {
        return answerType switch
        {
            GoogleOmniboxAnswerType.Dictionary => Strings.AnswerTypeDictionary,
            GoogleOmniboxAnswerType.Finance => Strings.AnswerTypeFinance,
            GoogleOmniboxAnswerType.GenericAnswer => Strings.AnswerTypeGeneric,
            GoogleOmniboxAnswerType.Local => Strings.AnswerTypeLocal,
            GoogleOmniboxAnswerType.Sports => Strings.AnswerTypeSports,
            GoogleOmniboxAnswerType.SunriseSunset => Strings.AnswerTypeSunriseSunset,
            GoogleOmniboxAnswerType.Translation => Strings.AnswerTypeTranslation,
            GoogleOmniboxAnswerType.Weather => Strings.AnswerTypeWeather,
            GoogleOmniboxAnswerType.WhenIs => Strings.AnswerTypeWhenIs,
            GoogleOmniboxAnswerType.Currency => Strings.AnswerTypeCurrency,
            GoogleOmniboxAnswerType.LocalTime => Strings.AnswerTypeLocalTime,
            GoogleOmniboxAnswerType.PlayInstall => Strings.AnswerTypePlayInstall,
            _ => Strings.AnswerTypeGeneric,
        };
    }

    private static int ResolveSuggestionScore(string suggestType, GoogleOmniboxAnswerType answerType, int relevance, int index)
    {
        var relevanceBonus = relevance > 0 ? Math.Clamp(relevance / 200, 0, 8) : 0;
        if (suggestType.Equals("CALCULATOR", StringComparison.OrdinalIgnoreCase))
        {
            return 95 + relevanceBonus;
        }

        if (suggestType.Equals("NAVIGATION", StringComparison.OrdinalIgnoreCase) ||
            suggestType.Equals("PERSONALIZED_NAVIGATION", StringComparison.OrdinalIgnoreCase))
        {
            return 88 + relevanceBonus - index;
        }

        if (answerType != GoogleOmniboxAnswerType.Unspecified)
        {
            return 86 + relevanceBonus - index;
        }

        return 60 + relevanceBonus - index;
    }

    private static int ResolveOmniboxAnswerScore(string suggestType, GoogleOmniboxAnswerType answerType, int relevance, int index)
    {
        var relevanceBonus = relevance > 0 ? Math.Clamp(relevance / 250, 0, 6) : 0;
        if (suggestType.Equals("CALCULATOR", StringComparison.OrdinalIgnoreCase))
        {
            return 146 + relevanceBonus;
        }

        return answerType switch
        {
            GoogleOmniboxAnswerType.Translation => 145 + relevanceBonus - index,
            GoogleOmniboxAnswerType.Currency => 145 + relevanceBonus - index,
            GoogleOmniboxAnswerType.Finance => 145 + relevanceBonus - index,
            GoogleOmniboxAnswerType.Weather => 145 + relevanceBonus - index,
            GoogleOmniboxAnswerType.LocalTime => 145 + relevanceBonus - index,
            GoogleOmniboxAnswerType.Dictionary => 144 + relevanceBonus - index,
            GoogleOmniboxAnswerType.GenericAnswer => 144 + relevanceBonus - index,
            _ => 142 + relevanceBonus - index,
        };
    }

    private static string CleanOmniboxText(string? value)
    {
        return TextSanitizer.NormalizeWhitespace(TextSanitizer.FromSuggestionHtml(value ?? string.Empty));
    }
}
