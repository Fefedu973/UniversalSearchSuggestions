using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using UniversalSearchSuggestions.Core.Resources;
using UniversalSearchSuggestions.Icons;
using UniversalSearchSuggestions.Pages;

namespace UniversalSearchSuggestions.Commands;

internal sealed partial class ResetCacheCommand(UniversalSearchSuggestionsPage page) : InvokableCommand
{
    public override string Name => Strings.ConfirmResetConfirm;

    public override IconInfo Icon => AppIcons.ResetCache;

    public override ICommandResult Invoke()
    {
        page.ClearCache();
        return CommandResult.ShowToast(new ToastArgs
        {
            Message = Strings.CommandResetCacheDone,
            Result = CommandResult.GoHome(),
        });
    }
}
