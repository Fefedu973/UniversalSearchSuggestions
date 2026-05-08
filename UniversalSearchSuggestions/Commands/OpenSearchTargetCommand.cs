using System.Diagnostics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using UniversalSearchSuggestions.Core.Browsers;
using UniversalSearchSuggestions.Core.Search;

namespace UniversalSearchSuggestions.Commands;

internal sealed partial class OpenSearchTargetCommand(
    SearchSuggestion suggestion,
    SearchPreferences preferences) : InvokableCommand
{
    public override string Name => suggestion.IsNavigation ? "Ouvrir" : "Rechercher";

    public override ICommandResult Invoke()
    {
        var browser = BrowserInstallDetector.Resolve(preferences.BrowserId, preferences.CustomBrowserPath);
        OpenTarget(suggestion.TargetUri, browser);
        return CommandResult.Dismiss();
    }

    private static void OpenTarget(Uri target, BrowserTarget browser)
    {
        if (browser.Kind is BrowserKind.Default || string.IsNullOrWhiteSpace(browser.ExecutablePath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ToLaunchUri(target),
                UseShellExecute = true,
            });
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = browser.ExecutablePath,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(ToLaunchUri(target));
        Process.Start(startInfo);
    }

    private static string ToLaunchUri(Uri uri)
    {
        return uri.Scheme is "http" or "https" ? uri.AbsoluteUri : uri.ToString();
    }
}
