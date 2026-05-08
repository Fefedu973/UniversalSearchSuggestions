using System.Xml.Linq;
using UniversalSearchSuggestions.Core.Utilities;

namespace UniversalSearchSuggestions.Core.Search.Providers;

public sealed class XmlSuggestionProvider(
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
        var document = XDocument.Parse(payload);
        var definition = SearchEngineCatalog.Get(engine);
        var results = new List<SearchSuggestion>();

        foreach (var value in document.Descendants("suggestion")
            .Select(static element => element.Attribute("data")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            var clean = TextSanitizer.NormalizeWhitespace(TextSanitizer.FromSuggestionHtml(value!));
            if (string.IsNullOrWhiteSpace(clean))
            {
                continue;
            }

            results.Add(new SearchSuggestion
            {
                Title = clean,
                Query = clean,
                TargetUri = SearchEngineCatalog.BuildSearchUri(engine, clean),
                Engine = engine,
                SourceKind = SuggestionSourceKind.SearchEngine,
                Section = definition.SuggestionSection,
                TextToSuggest = clean,
                Score = 45 - results.Count,
            });

            if (results.Count >= preferences.MaxSuggestionsPerEngine)
            {
                break;
            }
        }

        return results;
    }
}
