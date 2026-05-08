using System.Net;
using System.Text.RegularExpressions;

namespace UniversalSearchSuggestions.Core.Utilities;

public static partial class TextSanitizer
{
    private const char NoBreakSpace = (char)0x00A0;
    private const char NarrowNoBreakSpace = (char)0x202F;
    private const char ZeroWidthSpace = (char)0x200B;
    private const char ByteOrderMark = (char)0xFEFF;

    public static string FromSuggestionHtml(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var withoutTags = HtmlTagRegex().Replace(value, string.Empty);
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return CollapseInvisibleSpaces(decoded).Trim();
    }

    public static string NormalizeWhitespace(string value)
    {
        return WhitespaceRegex().Replace(CollapseInvisibleSpaces(value).Trim(), " ");
    }

    private static string CollapseInvisibleSpaces(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value
            .Replace(NoBreakSpace, ' ')
            .Replace(NarrowNoBreakSpace, ' ')
            .Replace(ZeroWidthSpace, ' ')
            .Replace(ByteOrderMark, ' ');
    }

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("\\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
