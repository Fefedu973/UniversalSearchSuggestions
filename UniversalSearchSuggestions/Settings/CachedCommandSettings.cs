using Microsoft.CommandPalette.Extensions;

namespace UniversalSearchSuggestions.Settings;

internal sealed partial class CachedCommandSettings : ICommandSettings
{
    public CachedCommandSettings(ICommandSettings inner)
    {
        SettingsPage = inner.SettingsPage;
    }

    public IContentPage SettingsPage { get; }
}
