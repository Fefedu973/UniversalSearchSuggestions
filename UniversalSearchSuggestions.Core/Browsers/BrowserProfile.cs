namespace UniversalSearchSuggestions.Core.Browsers;

public sealed record BrowserProfile(
    string DisplayName,
    string Directory,
    bool IsDefault);
