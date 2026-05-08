using UniversalSearchSuggestions.Core.Search;
using UniversalSearchSuggestions.Core.Search.Providers;

namespace UniversalSearchSuggestions.Tests;

public sealed class GoogleSuggestionParserTests
{
    [Fact]
    public void ParseRichSuggestionsReadsDescriptionAndImage()
    {
        const string payload = """
            window.google.ac.h([[["abraham lincoln",46,[512,433],{"zh":"Abraham Lincoln","zi":"16e président des Etats-Unis","zs":"https://encrypted-tbn0.gstatic.com/images?q=tbn:test&s=10"}],["abraham lincoln\u003cb\u003e mort\u003c\/b\u003e",0,[512]]],{"q":"token"}])
            """;

        var suggestions = GoogleSuggestionParser.ParseRichSuggestions(payload, "abraham lincoln");

        Assert.Equal(2, suggestions.Count);
        Assert.Equal("Abraham Lincoln", suggestions[0].Title);
        Assert.Equal("16e président des Etats-Unis", suggestions[0].Description);
        Assert.Equal("https://encrypted-tbn0.gstatic.com/images?q=tbn:test&s=10", suggestions[0].ImageUrl);
        Assert.Equal("abraham lincoln mort", suggestions[1].Query);
    }

    [Fact]
    public void ParseDefaultSuggestionsReadsGwsWizRowsWithXssiGuard()
    {
        const string payload = """
            )]}'
            [[["gta 6 trailer 3",0,[3,143,362],{"zl":8}],["championnat monde ping pong",46,[3,143,362],{"zh":"Championnats du monde de tennis de table","zi":"Résultat enrichi","zs":"data:image/jpeg;base64,abc"}]],{}]
            """;

        var suggestions = GoogleSuggestionParser.ParseDefaultSuggestions(
            payload,
            new SearchPreferences { PrimaryEngine = SearchEngineKind.DuckDuckGo });

        Assert.Equal(2, suggestions.Count);
        Assert.Equal("gta 6 trailer 3", suggestions[0].Query);
        Assert.Equal("https://duckduckgo.com/?q=gta%206%20trailer%203", suggestions[0].TargetUri.AbsoluteUri);
        Assert.Equal("Championnats du monde de tennis de table", suggestions[1].Title);
        Assert.Equal("Résultat enrichi", suggestions[1].Description);
        Assert.Equal("data:image/jpeg;base64,abc", suggestions[1].ImageUrl);
    }

    [Fact]
    public void ParseRichSuggestionsStripsHtmlTagsAndDecodesEntities()
    {
        const string payload = """
            window.google.ac.h([[["paris",46,[],{"zh":"<b>Paris<\/b>","zi":"capitale de&nbsp;la&nbsp;France","zs":"https://example.com/p.jpg"}]],{}])
            """;

        var suggestions = GoogleSuggestionParser.ParseRichSuggestions(payload, "paris");

        Assert.Single(suggestions);
        Assert.Equal("Paris", suggestions[0].Title);
        Assert.Equal("capitale de la France", suggestions[0].Description);
    }

    [Fact]
    public void ParseChromeSuggestionsMarksCalculatorAndNavigation()
    {
        const string payload = """
            ["1+1",["\u003d 2","https://www.youtube.com/"],["Calculator","YouTube"],[],{"google:suggesttype":["CALCULATOR","NAVIGATION"]}]
            """;

        var suggestions = GoogleSuggestionParser.ParseChromeSuggestions(payload, "1+1");

        Assert.Equal("1+1 = 2", suggestions[0].Title);
        Assert.Equal("2", suggestions[0].Query);
        Assert.Equal("Calculator", suggestions[0].Description);
        Assert.False(suggestions[0].IsNavigation);
        Assert.True(suggestions[1].IsNavigation);
        Assert.Equal(SuggestionSourceKind.SearchEngine, suggestions[1].SourceKind);
        Assert.Equal("https://www.youtube.com/", suggestions[1].TargetUri.ToString());
    }

    [Fact]
    public void ParseChromeOmniboxAnswerSuggestionsReadsCalculatorAnswer()
    {
        const string payload = """
            ["1+1",["\u003d 2","1+1"],["Calculator",""],[],{"google:suggesttype":["CALCULATOR","QUERY"]}]
            """;

        var suggestions = GoogleSuggestionParser.ParseChromeOmniboxAnswerSuggestions(payload, "1+1");

        var answer = Assert.Single(suggestions);
        Assert.Equal("1+1 = 2", answer.Title);
        Assert.Equal("= 2", answer.Query);
        Assert.Equal("2", answer.TextToSuggest);
        Assert.Equal(SuggestionSourceKind.SearchAnswer, answer.SourceKind);
        Assert.Equal("calculator", answer.IconHint);
        Assert.Equal("https://www.google.com/search?q=1%2B1", answer.TargetUri.AbsoluteUri);
    }

    [Fact]
    public void ParseChromeOmniboxAnswerSuggestionsReadsInlineAnswerPayload()
    {
        const string payload = """
            ["capitale de la france",["capitale de la france"],[""],[],{"google:suggestdetail":[{"ansa":{"l":[{"il":{"t":[{"t":"capitale de la france","tt":8}]}},{"il":{"t":[{"t":"Paris","tt":2}]}}]},"ansb":"3"}],"google:suggesttype":["QUERY"]}]
            """;

        var suggestions = GoogleSuggestionParser.ParseChromeOmniboxAnswerSuggestions(payload, "capitale de la france");

        var answer = Assert.Single(suggestions);
        Assert.Equal("capitale de la france", answer.Query);
        Assert.Equal("Paris", answer.Description);
        Assert.Equal(SuggestionSourceKind.SearchAnswer, answer.SourceKind);
    }

    [Fact]
    public void ParseChromeOmniboxAnswerSuggestionsUsesDetailDisplayText()
    {
        const string payload = """
            )]}' garbage ["heure à Tokyo",["heure à Tokyo"],[""],[],{"google:suggestdetail":[{"t":"Tokyo · 17:42","ansa":{"l":[{"il":{"t":[{"t":"heure à Tokyo","tt":8}]}},{"il":{"t":[{"t":"17:42","tt":2}]}}]},"ansb":"11"}],"google:suggesttype":["QUERY"],"google:suggestrelevance":[1300]}]
            """;

        var suggestions = GoogleSuggestionParser.ParseChromeOmniboxAnswerSuggestions(payload, "heure à Tokyo");

        var answer = Assert.Single(suggestions);
        Assert.Equal("Tokyo · 17:42", answer.Title);
        Assert.Equal("17:42", answer.Description);
        Assert.Equal("time", answer.IconHint);
    }

    [Fact]
    public void ParseChromeOmniboxAnswerSuggestionsReadsFinanceAnswerType()
    {
        const string payload = """
            ["AAPL stock",["AAPL stock"],[""],[],{"google:suggestdetail":[{"ansa":{"l":[{"il":{"t":[{"t":"AAPL stock","tt":8}]}},{"il":{"t":[{"t":"Apple Inc","tt":2},{"t":"$190.12","tt":2},{"t":"+1.25%","tt":2}]}}]},"ansb":"2"}],"google:suggesttype":["QUERY"]}]
            """;

        var suggestions = GoogleSuggestionParser.ParseChromeOmniboxAnswerSuggestions(payload, "AAPL stock");

        var answer = Assert.Single(suggestions);
        Assert.Equal("Apple Inc - $190.12 - +1.25%", answer.Description);
        Assert.Equal("finance", answer.IconHint);
    }

    [Fact]
    public void ParseChromeOmniboxAnswerSuggestionsReadsNumericDictionaryAnswerType()
    {
        const string payload = """
            ["define serendipity",["define serendipity"],[""],[],{"google:suggestdetail":[{"ansa":{"l":[{"il":{"t":[{"t":"define serendipity","tt":8}]}},{"il":{"t":[{"t":"the occurrence of events by chance in a happy way","tt":2}]}}]},"ansb":1}],"google:suggesttype":["QUERY"]}]
            """;

        var suggestions = GoogleSuggestionParser.ParseChromeOmniboxAnswerSuggestions(payload, "define serendipity");

        var answer = Assert.Single(suggestions);
        Assert.Equal("the occurrence of events by chance in a happy way", answer.Description);
        Assert.Equal("dictionary", answer.IconHint);
    }

    [Fact]
    public void ParseToolbarSuggestionsReadsGoogleXmlApi()
    {
        const string payload = """
            <toplevel><CompleteSuggestion><suggestion data="bonjour"/></CompleteSuggestion></toplevel>
            """;

        var suggestions = GoogleSuggestionParser.ParseToolbarSuggestions(payload);

        var suggestion = Assert.Single(suggestions);
        Assert.Equal("bonjour", suggestion.Query);
        Assert.Equal(SearchEngineKind.Google, suggestion.Engine);
    }
}
