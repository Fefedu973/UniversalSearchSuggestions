using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using UniversalSearchSuggestions.Core.Resources;
using UniversalSearchSuggestions.Icons;
using UniversalSearchSuggestions.Pages;

namespace UniversalSearchSuggestions.Commands;

internal sealed partial class ConfirmedResetCacheCommand(UniversalSearchSuggestionsPage page) : InvokableCommand
{
    private readonly ResetCacheCommand _innerReset = new(page);

    public override string Name => Strings.CommandResetCache;

    public override IconInfo Icon => AppIcons.ResetCache;

    public override ICommandResult Invoke()
    {
        return CommandResult.Confirm(new ConfirmationArgs
        {
            Title = Strings.ConfirmResetTitle,
            Description = Strings.ConfirmResetDescription,
            IsPrimaryCommandCritical = true,
            PrimaryCommand = _innerReset,
        });
    }
}
