using UniversalSearchSuggestions.Core.Utilities;

namespace UniversalSearchSuggestions.Tests;

public sealed class FaviconResolverTests
{
    [Fact]
    public void BuildGoogleFaviconUrlUsesOriginAndSize()
    {
        var faviconUrl = FaviconResolver.BuildGoogleFaviconUrl(new Uri("https://www.youtube.com/watch?v=abc"), 96);

        Assert.Equal(
            "https://www.google.com/s2/favicons?domain_url=https%3A%2F%2Fwww.youtube.com%2F&sz=96",
            faviconUrl);
    }

    [Fact]
    public void BuildGoogleFaviconUrlSkipsLoopback()
    {
        var faviconUrl = FaviconResolver.BuildGoogleFaviconUrl(new Uri("http://localhost:3000"));

        Assert.Null(faviconUrl);
    }
}
