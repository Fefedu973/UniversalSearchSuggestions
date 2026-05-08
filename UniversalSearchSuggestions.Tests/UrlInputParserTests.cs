using UniversalSearchSuggestions.Core.Utilities;

namespace UniversalSearchSuggestions.Tests;

public sealed class UrlInputParserTests
{
    [Theory]
    [InlineData("youtube.com", "https://youtube.com/")]
    [InlineData("www.youtube.com/watch?v=abc", "https://www.youtube.com/watch?v=abc")]
    [InlineData("http://localhost:3000/app", "http://localhost:3000/app")]
    [InlineData("127.0.0.1:5000", "https://127.0.0.1:5000/")]
    public void TryParseDetectsDirectNavigation(string input, string expected)
    {
        Assert.True(UrlInputParser.TryParse(input, out var uri));
        Assert.Equal(expected, uri.ToString());
    }

    [Theory]
    [InlineData("1+1")]
    [InlineData("weather paris")]
    [InlineData("hello")]
    [InlineData("1.1")]
    public void TryParseRejectsRegularQueries(string input)
    {
        Assert.False(UrlInputParser.TryParse(input, out _));
    }
}
