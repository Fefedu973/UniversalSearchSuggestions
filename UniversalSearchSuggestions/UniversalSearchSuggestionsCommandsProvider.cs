// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using UniversalSearchSuggestions.Commands;
using UniversalSearchSuggestions.Core.Resources;
using UniversalSearchSuggestions.Icons;
using UniversalSearchSuggestions.Pages;
using UniversalSearchSuggestions.Settings;

namespace UniversalSearchSuggestions;

public sealed partial class UniversalSearchSuggestionsCommandsProvider : CommandProvider
{
    public const string ProviderId = "com.fefedu973.universalsearchsuggestions";
    public const string ResetCommandId = ProviderId + ".reset-cache";
    public const string DockBandId = ProviderId + ".dock-band";
    public const string FallbackId = ProviderId + ".fallback";

    private readonly ICommandItem[] _commands;
    private readonly IFallbackCommandItem[] _fallbackCommands;
    private readonly ICommandItem[] _dockBands;
    private readonly SearchSettingsManager _settingsManager = new();
    private readonly UniversalSearchSuggestionsPage _page;
    private readonly ConfirmedResetCacheCommand _resetCacheCommand;

    public UniversalSearchSuggestionsCommandsProvider()
    {
        Id = ProviderId;
        DisplayName = Strings.DockTitle;
        Icon = AppIcons.ExtensionLogo;
        Settings = new CachedCommandSettings(_settingsManager.Settings);
        _page = new UniversalSearchSuggestionsPage(_settingsManager);
        _resetCacheCommand = new ConfirmedResetCacheCommand(_page) { Id = ResetCommandId };

        _commands =
        [
            new CommandItem(_page)
            {
                Title = DisplayName,
                MoreCommands =
                [
                    new CommandContextItem(_resetCacheCommand)
                    {
                        Title = Strings.CommandResetCache,
                        Subtitle = Strings.CommandResetCacheSubtitle,
                        Icon = AppIcons.ResetCache,
                        IsCritical = true,
                    },
                ],
            },
        ];

        _fallbackCommands =
        [
            new SearchFallbackCommandItem(_settingsManager),
        ];

        var dockLauncher = new ListItem(_page)
        {
            Title = Strings.DockTitle,
            Subtitle = Strings.DockSubtitle,
            Icon = AppIcons.ExtensionLogo,
        };
        _dockBands =
        [
            new WrappedDockItem([dockLauncher], DockBandId, Strings.DockTitle),
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

    public override IFallbackCommandItem[]? FallbackCommands()
    {
        return _fallbackCommands;
    }

    public override ICommandItem[]? GetDockBands()
    {
        return _dockBands;
    }

    public override void InitializeWithHost(IExtensionHost host)
    {
        base.InitializeWithHost(host);
        _page.SetExtensionHost(host);
    }

    public override void Dispose()
    {
        _settingsManager.SaveSettings();
        _page.SetExtensionHost(null);
        _page.Dispose();
        base.Dispose();
    }
}
