namespace UniversalSearchSuggestions.Pages;

internal enum SuggestionFilterKind
{
    All,
    Web,
    Local,
    Answers,
    Navigation,
}

internal static class SuggestionFilterIds
{
    public const string All = "all";
    public const string Web = "web";
    public const string Local = "local";
    public const string Answers = "answers";
    public const string Navigation = "navigation";

    public static SuggestionFilterKind Parse(string? id)
    {
        return id switch
        {
            Web => SuggestionFilterKind.Web,
            Local => SuggestionFilterKind.Local,
            Answers => SuggestionFilterKind.Answers,
            Navigation => SuggestionFilterKind.Navigation,
            _ => SuggestionFilterKind.All,
        };
    }
}
