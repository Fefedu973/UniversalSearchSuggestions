namespace UniversalSearchSuggestions.Core.Search.Providers;

public interface ISuggestionProvider
{
    SearchEngineKind Engine { get; }

    Task<IReadOnlyList<SearchSuggestion>> GetSuggestionsAsync(
        string query,
        SearchPreferences preferences,
        CancellationToken cancellationToken);
}
