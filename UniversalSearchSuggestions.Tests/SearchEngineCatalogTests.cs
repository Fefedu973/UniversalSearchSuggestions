using UniversalSearchSuggestions.Core.Search;

namespace UniversalSearchSuggestions.Tests;

public sealed class SearchEngineCatalogTests
{
    [Fact]
    public void BuildFromTemplateSupportsQueryTokens()
    {
        var uri = SearchEngineCatalog.BuildFromTemplate("https://example.com/search?q={query+}&src=%s", "hello world");

        Assert.Equal("https://example.com/search?q=hello+world&src=hello%20world", uri.AbsoluteUri);
    }

    [Fact]
    public void BuildSearchUriUsesPrimaryEngineTemplate()
    {
        var uri = SearchEngineCatalog.BuildSearchUri(SearchEngineKind.Brave, "universal search");

        Assert.Equal("https://search.brave.com/search?q=universal%20search", uri.AbsoluteUri);
    }

    [Theory]
    [InlineData(SearchEngineKind.Yahoo, "https://search.yahoo.com/search?p=hello")]
    [InlineData(SearchEngineKind.Baidu, "https://www.baidu.com/s?wd=hello")]
    [InlineData(SearchEngineKind.Yandex, "https://yandex.com/search/?text=hello")]
    [InlineData(SearchEngineKind.Naver, "https://search.naver.com/search.naver?query=hello")]
    [InlineData(SearchEngineKind.Ask, "https://www.ask.com/web?q=hello")]
    [InlineData(SearchEngineKind.Ecosia, "https://www.ecosia.org/search?q=hello")]
    [InlineData(SearchEngineKind.Qwant, "https://www.qwant.com/?q=hello")]
    [InlineData(SearchEngineKind.Startpage, "https://www.startpage.com/do/dsearch?query=hello")]
    [InlineData(SearchEngineKind.Swisscows, "https://swisscows.com/web?query=hello")]
    [InlineData(SearchEngineKind.Dogpile, "https://www.dogpile.com/serp?q=hello")]
    [InlineData(SearchEngineKind.Gibiru, "https://gibiru.com/results.html?q=hello")]
    [InlineData(SearchEngineKind.Mojeek, "https://www.mojeek.com/search?q=hello")]
    [InlineData(SearchEngineKind.MetaGer, "https://metager.org/meta/meta.ger3?eingabe=hello")]
    [InlineData(SearchEngineKind.ZapMeta, "https://www.zapmeta.com/search?q=hello")]
    [InlineData(SearchEngineKind.SearchEncrypt, "https://www.searchencrypt.com/search?q=hello")]
    [InlineData(SearchEngineKind.OneSearch, "https://www.onesearch.com/yhs/search?q=hello")]
    [InlineData(SearchEngineKind.Ekoru, "https://ekoru.org/search?q=hello")]
    public void BuildSearchUriSupportsLegacyConfiguredEngines(SearchEngineKind engine, string expected)
    {
        var uri = SearchEngineCatalog.BuildSearchUri(engine, "hello");

        Assert.Equal(expected, uri.AbsoluteUri);
    }
}
