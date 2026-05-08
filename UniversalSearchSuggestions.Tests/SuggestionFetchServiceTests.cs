using UniversalSearchSuggestions.Core.Search;
using UniversalSearchSuggestions.Core.Search.Providers;

namespace UniversalSearchSuggestions.Tests;

public sealed class SuggestionFetchServiceTests
{
    [Fact]
    public void BuildImmediateSuggestionsReturnsSearchActionWithoutWaitingForProviders()
    {
        var suggestions = SuggestionFetchService.BuildImmediateSuggestions(
            "open ai",
            new SearchPreferences { PrimaryEngine = SearchEngineKind.Google });

        Assert.Single(suggestions);
        Assert.Equal("Rechercher \"open ai\"", suggestions[0].Title);
        Assert.Equal("https://www.google.com/search?q=open%20ai", suggestions[0].TargetUri.AbsoluteUri);
        Assert.True(suggestions[0].IsCurrentQueryAction);
    }

    [Fact]
    public void BuildImmediateSuggestionsReturnsDirectUrlBeforeSearchAction()
    {
        var suggestions = SuggestionFetchService.BuildImmediateSuggestions(
            "youtube.com",
            new SearchPreferences { PrimaryEngine = SearchEngineKind.Google });

        Assert.Equal(2, suggestions.Count);
        Assert.Equal(SuggestionSourceKind.DirectUrl, suggestions[0].SourceKind);
        Assert.Equal("https://youtube.com/", suggestions[0].TargetUri.AbsoluteUri);
        Assert.True(suggestions[1].IsCurrentQueryAction);
    }

    [Fact]
    public async Task SearchAsyncRedirectsProviderTextSuggestionsThroughPrimaryEngine()
    {
        var service = new SuggestionFetchService([new StaticSuggestionProvider()]);
        var preferences = new SearchPreferences
        {
            PrimaryEngine = SearchEngineKind.DuckDuckGo,
            EnableBing = true,
            EnableGoogle = false,
            EnableDuckDuckGo = false,
            EnableBrave = false,
            IncludeBrowserBookmarks = false,
            IncludeBrowserHistory = false,
        };

        var suggestions = await service.SearchAsync("docs", preferences, CancellationToken.None);

        var providerSuggestion = Assert.Single(suggestions, suggestion => suggestion.Query == "docs api");
        Assert.Equal(SearchEngineKind.Bing, providerSuggestion.Engine);
        Assert.Equal("https://duckduckgo.com/?q=docs%20api", providerSuggestion.TargetUri.AbsoluteUri);
    }

    [Fact]
    public async Task SearchAsyncEnrichesImmediateSearchActionWithExactProviderResult()
    {
        var service = new SuggestionFetchService([new ExactRichSuggestionProvider()]);
        var preferences = new SearchPreferences
        {
            PrimaryEngine = SearchEngineKind.Google,
            EnableBing = true,
            EnableGoogle = false,
            EnableDuckDuckGo = false,
            EnableBrave = false,
            IncludeBrowserBookmarks = false,
            IncludeBrowserHistory = false,
        };

        var suggestions = await service.SearchAsync("capitale de la france", preferences, CancellationToken.None);

        var currentTyped = Assert.Single(suggestions, suggestion => suggestion.Title == "Rechercher \"capitale de la france\"");
        Assert.Equal("Paris", currentTyped.Description);
        Assert.Equal("https://example.test/paris.png", currentTyped.ImageUrl);
        Assert.True(currentTyped.IsCurrentQueryAction);
    }

    private sealed class StaticSuggestionProvider : ISuggestionProvider
    {
        public SearchEngineKind Engine => SearchEngineKind.Bing;

        public Task<IReadOnlyList<SearchSuggestion>> GetSuggestionsAsync(
            string query,
            SearchPreferences preferences,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<SearchSuggestion>>(
            [
                new SearchSuggestion
                {
                    Title = "docs api",
                    Query = "docs api",
                    TargetUri = SearchEngineCatalog.BuildSearchUri(SearchEngineKind.Bing, "docs api"),
                    Engine = SearchEngineKind.Bing,
                    SourceKind = SuggestionSourceKind.SearchEngine,
                    TextToSuggest = "docs api",
                    Score = 50,
                },
            ]);
        }
    }

    private sealed class ExactRichSuggestionProvider : ISuggestionProvider
    {
        public SearchEngineKind Engine => SearchEngineKind.Bing;

        public Task<IReadOnlyList<SearchSuggestion>> GetSuggestionsAsync(
            string query,
            SearchPreferences preferences,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<SearchSuggestion>>(
            [
                new SearchSuggestion
                {
                    Title = query,
                    Query = query,
                    TargetUri = SearchEngineCatalog.BuildSearchUri(SearchEngineKind.Bing, query),
                    Engine = SearchEngineKind.Bing,
                    SourceKind = SuggestionSourceKind.SearchEngine,
                    Description = "Paris",
                    ImageUrl = "https://example.test/paris.png",
                    TextToSuggest = query,
                    Score = 50,
                },
            ]);
        }
    }
}
