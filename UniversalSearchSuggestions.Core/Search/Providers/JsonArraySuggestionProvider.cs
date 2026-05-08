using System.Text.Json;
using UniversalSearchSuggestions.Core.Utilities;

namespace UniversalSearchSuggestions.Core.Search.Providers;

public sealed class JsonArraySuggestionProvider(
    SearchEngineKind engine,
    HttpClient httpClient,
    Func<string, string, Uri> endpointFactory) : ISuggestionProvider
{
    public SearchEngineKind Engine => engine;

    public async Task<IReadOnlyList<SearchSuggestion>> GetSuggestionsAsync(
        string query,
        SearchPreferences preferences,
        CancellationToken cancellationToken)
    {
        var endpoint = endpointFactory(query, preferences.Language);
        var payload = await httpClient.GetStringAsync(endpoint, cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(payload);

        var values = ExtractSuggestions(document.RootElement);
        var definition = SearchEngineCatalog.Get(engine);
        var results = new List<SearchSuggestion>();

        foreach (var value in values)
        {
            var clean = TextSanitizer.NormalizeWhitespace(TextSanitizer.FromSuggestionHtml(value.Query));
            if (string.IsNullOrWhiteSpace(clean))
            {
                continue;
            }

            results.Add(new SearchSuggestion
            {
                Title = TextSanitizer.NormalizeWhitespace(TextSanitizer.FromSuggestionHtml(value.Title ?? string.Empty)) is { Length: > 0 } title ? title : clean,
                Query = clean,
                TargetUri = SearchEngineCatalog.BuildSearchUri(engine, clean),
                Engine = engine,
                SourceKind = SuggestionSourceKind.SearchEngine,
                Description = NormalizeOptional(value.Description),
                ImageUrl = value.ImageUrl,
                Section = definition.SuggestionSection,
                TextToSuggest = clean,
                IconHint = value.IsEntity ? "answer" : null,
                Score = 50 - results.Count,
            });

            if (results.Count >= preferences.MaxSuggestionsPerEngine)
            {
                break;
            }
        }

        return results;
    }

    private static IEnumerable<SuggestionCandidate> ExtractSuggestions(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var value in ExtractObjectSuggestions(root))
            {
                yield return value;
            }

            yield break;
        }

        if (root.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        if (root.GetArrayLength() > 1 && root[1].ValueKind == JsonValueKind.Array)
        {
            foreach (var value in ExtractArray(root[1]))
            {
                yield return value;
            }

            yield break;
        }

        foreach (var value in ExtractArray(root))
        {
            yield return value;
        }
    }

    private static IEnumerable<SuggestionCandidate> ExtractObjectSuggestions(JsonElement root)
    {
        if (root.TryGetProperty("gossip", out var gossip) &&
            gossip.ValueKind == JsonValueKind.Object &&
            gossip.TryGetProperty("results", out var yahooResults))
        {
            foreach (var value in ExtractArray(yahooResults))
            {
                yield return value;
            }
        }

        if (root.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("items", out var qwantItems))
        {
            foreach (var value in ExtractArray(qwantItems))
            {
                yield return value;
            }
        }

        if (root.TryGetProperty("suggestions", out var ecosiaSuggestions))
        {
            foreach (var value in ExtractArray(ecosiaSuggestions))
            {
                yield return value;
            }
        }

        if (root.TryGetProperty("suggestionGroups", out var suggestionGroups) &&
            suggestionGroups.ValueKind == JsonValueKind.Array)
        {
            foreach (var group in suggestionGroups.EnumerateArray())
            {
                if (group.ValueKind == JsonValueKind.Object &&
                    group.TryGetProperty("searchSuggestions", out var searchSuggestions))
                {
                    foreach (var value in ExtractArray(searchSuggestions))
                    {
                        yield return value;
                    }
                }
            }
        }
    }

    private static IEnumerable<SuggestionCandidate> ExtractArray(JsonElement suggestionArray)
    {
        if (suggestionArray.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var element in suggestionArray.EnumerateArray())
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    yield return new SuggestionCandidate(element.GetString() ?? string.Empty);
                    break;
                case JsonValueKind.Object when element.TryGetProperty("phrase", out var phrase) && phrase.ValueKind == JsonValueKind.String:
                    yield return ReadObjectCandidate(element, phrase.GetString() ?? string.Empty);
                    break;
                case JsonValueKind.Object:
                    var query = TryGetString(element, "q") ??
                        TryGetString(element, "query") ??
                        TryGetString(element, "key") ??
                        TryGetString(element, "value") ??
                        TryGetString(element, "displayText") ??
                        TryGetString(element, "name") ??
                        TryGetString(element, "title");
                    if (!string.IsNullOrWhiteSpace(query))
                    {
                        yield return ReadObjectCandidate(element, query);
                    }

                    break;
            }
        }
    }

    private static SuggestionCandidate ReadObjectCandidate(JsonElement element, string query)
    {
        return new SuggestionCandidate(
            query,
            TryGetString(element, "name") ?? TryGetString(element, "title") ?? TryGetString(element, "displayText") ?? query,
            TryGetString(element, "desc") ?? TryGetString(element, "description") ?? TryGetString(element, "subtitle"),
            TryGetString(element, "img") ?? TryGetString(element, "image") ?? TryGetString(element, "imageUrl"),
            TryGetBool(element, "is_entity"));
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
    }

    private static bool TryGetBool(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.True;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = TextSanitizer.NormalizeWhitespace(TextSanitizer.FromSuggestionHtml(value ?? string.Empty));
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private sealed record SuggestionCandidate(
        string Query,
        string? Title = null,
        string? Description = null,
        string? ImageUrl = null,
        bool IsEntity = false);
}
