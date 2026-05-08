using System.Net;
using UniversalSearchSuggestions.Core.Search;
using UniversalSearchSuggestions.Core.Search.Providers;

namespace UniversalSearchSuggestions.Tests;

public sealed class JsonArraySuggestionProviderTests
{
    [Theory]
    [InlineData(SearchEngineKind.Yahoo, """{"gossip":{"results":[{"key":"speed test"}]}}""", "speed test")]
    [InlineData(SearchEngineKind.Ecosia, """{"suggestions":["test debit"]}""", "test debit")]
    [InlineData(SearchEngineKind.Qwant, """{"data":{"items":[{"value":"test clavier"}]}}""", "test clavier")]
    [InlineData(SearchEngineKind.Swisscows, """["test","testbook"]""", "test")]
    public async Task ProviderReadsLegacySuggestionShapes(SearchEngineKind engine, string payload, string expected)
    {
        var provider = new JsonArraySuggestionProvider(
            engine,
            new HttpClient(new StaticHandler(payload)),
            static (_, _) => new Uri("https://example.test/suggest"));

        var suggestions = await provider.GetSuggestionsAsync(
            "test",
            new SearchPreferences { MaxSuggestionsPerEngine = 5 },
            CancellationToken.None);

        Assert.Contains(suggestions, suggestion => suggestion.Query == expected);
    }

    [Fact]
    public async Task ProviderReadsBraveRichEntity()
    {
        const string payload = """
            ["github",[{"is_entity":true,"name":"GitHub","desc":"Plateforme de développement","img":"https://example.test/github.png"},{"q":"github login"}]]
            """;

        var provider = new JsonArraySuggestionProvider(
            SearchEngineKind.Brave,
            new HttpClient(new StaticHandler(payload)),
            static (_, _) => new Uri("https://example.test/suggest"));

        var suggestions = await provider.GetSuggestionsAsync(
            "github",
            new SearchPreferences { MaxSuggestionsPerEngine = 5 },
            CancellationToken.None);

        var entity = Assert.Single(suggestions, suggestion => suggestion.Title == "GitHub");
        Assert.Equal("Plateforme de développement", entity.Description);
        Assert.Equal("https://example.test/github.png", entity.ImageUrl);
    }

    private sealed class StaticHandler(string payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload),
            });
        }
    }
}
