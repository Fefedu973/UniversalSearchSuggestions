using System.Globalization;

namespace UniversalSearchSuggestions.Core.Browsers;

public static class LocalSearchQueryParser
{
    public static ParsedLocalSearchQuery Parse(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new ParsedLocalSearchQuery([], []);
        }

        var includeTerms = new List<string>();
        var excludeTerms = new List<string>();
        foreach (var rawTerm in query.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var term = rawTerm.ToLower(CultureInfo.CurrentCulture);
            if (term.StartsWith("\\-", StringComparison.Ordinal) && term.Length > 2)
            {
                includeTerms.Add(term[1..]);
            }
            else if (term.Length > 1 && term[0] == '-')
            {
                excludeTerms.Add(term[1..]);
            }
            else if (term != "-")
            {
                includeTerms.Add(term);
            }
        }

        return new ParsedLocalSearchQuery(includeTerms, excludeTerms);
    }

    public static bool Matches(string searchableText, ParsedLocalSearchQuery query)
    {
        var normalized = searchableText.ToLower(CultureInfo.CurrentCulture);
        return (query.IncludeTerms.Count == 0 || query.IncludeTerms.All(normalized.Contains)) &&
            (query.ExcludeTerms.Count == 0 || !query.ExcludeTerms.Any(normalized.Contains));
    }
}
