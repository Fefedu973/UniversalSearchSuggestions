namespace UniversalSearchSuggestions.Core.Browsers;

public sealed record ParsedLocalSearchQuery(
    IReadOnlyList<string> IncludeTerms,
    IReadOnlyList<string> ExcludeTerms)
{
    public bool IsEmpty => IncludeTerms.Count == 0 && ExcludeTerms.Count == 0;
}
