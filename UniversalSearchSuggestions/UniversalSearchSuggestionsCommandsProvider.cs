// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using UniversalSearchSuggestions.Icons;
using UniversalSearchSuggestions.Pages;
using UniversalSearchSuggestions.Settings;

namespace UniversalSearchSuggestions;

public sealed partial class UniversalSearchSuggestionsCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly SearchSettingsManager _settingsManager = new();
    private readonly UniversalSearchSuggestionsPage _page;

    public UniversalSearchSuggestionsCommandsProvider()
    {
        DisplayName = "Universal Search Suggestions";
        Icon = AppIcons.ExtensionLogo;
        Settings = new CachedCommandSettings(_settingsManager.Settings);
        _page = new UniversalSearchSuggestionsPage(_settingsManager);
        _commands = [
            new CommandItem(_page) { Title = DisplayName },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

    public override void Dispose()
    {
        _settingsManager.SaveSettings();
        _page.Dispose();
        base.Dispose();
    }
}
