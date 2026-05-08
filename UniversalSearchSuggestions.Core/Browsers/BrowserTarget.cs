namespace UniversalSearchSuggestions.Core.Browsers;

public sealed record BrowserTarget(
    string Id,
    BrowserKind Kind,
    string DisplayName,
    string? ExecutablePath,
    string? UserDataPath);
