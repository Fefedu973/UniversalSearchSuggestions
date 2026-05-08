namespace UniversalSearchSuggestions.Core.Search;

public sealed record SearchSuggestion
{
    public required string Title { get; init; }

    public required string Query { get; init; }

    public required Uri TargetUri { get; init; }

    public required SearchEngineKind Engine { get; init; }

    public required SuggestionSourceKind SourceKind { get; init; }

    public string? Description { get; init; }

    public string? ImageUrl { get; init; }

    public string? Section { get; init; }

    public string? TextToSuggest { get; init; }

    public string? BrowserName { get; init; }

    public string? IconHint { get; init; }

    public double Score { get; init; }

    public bool IsNavigation { get; init; }

    public bool IsCurrentQueryAction { get; init; }
}
