using UniversalSearchSuggestions.Core.Search;

namespace UniversalSearchSuggestions.Core.Browsers;

public sealed record BrowserLocalEntry(
    string Title,
    Uri TargetUri,
    string BrowserName,
    SuggestionSourceKind SourceKind,
    int VisitCount = 0,
    int TypedCount = 0,
    DateTimeOffset? LastVisited = null)
{
    public string SearchableText => $"{Title} {TargetUri}";
}
