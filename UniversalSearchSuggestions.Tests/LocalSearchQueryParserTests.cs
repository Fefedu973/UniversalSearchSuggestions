using UniversalSearchSuggestions.Core.Browsers;

namespace UniversalSearchSuggestions.Tests;

public sealed class LocalSearchQueryParserTests
{
    [Fact]
    public void ParseSupportsIncludeExcludeAndEscapedDash()
    {
        var parsed = LocalSearchQueryParser.Parse("github docs -old \\-literal");

        Assert.Equal(["github", "docs", "-literal"], parsed.IncludeTerms);
        Assert.Equal(["old"], parsed.ExcludeTerms);
    }

    [Fact]
    public void MatchesRequiresAllIncludeTermsAndNoExcludeTerms()
    {
        var parsed = LocalSearchQueryParser.Parse("github docs -old");

        Assert.True(LocalSearchQueryParser.Matches("GitHub API docs", parsed));
        Assert.False(LocalSearchQueryParser.Matches("GitHub old docs", parsed));
        Assert.False(LocalSearchQueryParser.Matches("GitHub issues", parsed));
    }
}
