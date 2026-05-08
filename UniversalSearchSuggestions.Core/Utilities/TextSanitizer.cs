using System.Net;
using System.Text.RegularExpressions;

namespace UniversalSearchSuggestions.Core.Utilities;

public static partial class TextSanitizer
{
    public static string FromSuggestionHtml(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var withoutTags = HtmlTagRegex().Replace(value, string.Empty);
        return WebUtility.HtmlDecode(withoutTags).Trim();
    }

    public static string NormalizeWhitespace(string value)
    {
        return WhitespaceRegex().Replace(value.Trim(), " ");
    }

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("\\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
