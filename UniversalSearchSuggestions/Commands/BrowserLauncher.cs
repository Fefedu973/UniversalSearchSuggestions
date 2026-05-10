using System.Diagnostics;
using UniversalSearchSuggestions.Core.Browsers;

namespace UniversalSearchSuggestions.Commands;

internal static class BrowserLauncher
{
    public static void Open(Uri target, BrowserTarget browser, BrowserProfile? profile, bool privateMode)
    {
        var launchUri = ToLaunchUri(target);
        var canCustomize = profile is not null || privateMode;

        if (!canCustomize && (browser.Kind is BrowserKind.Default || string.IsNullOrWhiteSpace(browser.ExecutablePath)))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = launchUri,
                UseShellExecute = true,
            });
            return;
        }

        if (string.IsNullOrWhiteSpace(browser.ExecutablePath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = launchUri,
                UseShellExecute = true,
            });
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = browser.ExecutablePath,
            UseShellExecute = false,
        };

        AppendBrowserArguments(startInfo, browser.Kind, profile, privateMode);
        startInfo.ArgumentList.Add(launchUri);
        Process.Start(startInfo);
    }

    private static void AppendBrowserArguments(
        ProcessStartInfo startInfo,
        BrowserKind kind,
        BrowserProfile? profile,
        bool privateMode)
    {
        switch (kind)
        {
            case BrowserKind.Chrome:
            case BrowserKind.Edge:
            case BrowserKind.Brave:
                if (profile is not null && !string.IsNullOrWhiteSpace(profile.Directory))
                {
                    startInfo.ArgumentList.Add($"--profile-directory={profile.Directory}");
                }

                if (privateMode)
                {
                    startInfo.ArgumentList.Add(kind == BrowserKind.Edge ? "--inprivate" : "--incognito");
                }

                break;

            case BrowserKind.Firefox:
                if (profile is not null && !string.IsNullOrWhiteSpace(profile.DisplayName))
                {
                    // -P alone is a no-op when Firefox is already running with another profile,
                    // so pair it with -no-remote to actually launch the requested profile.
                    startInfo.ArgumentList.Add("-no-remote");
                    startInfo.ArgumentList.Add("-P");
                    startInfo.ArgumentList.Add(profile.DisplayName);
                }

                if (privateMode)
                {
                    startInfo.ArgumentList.Add("-private-window");
                }

                break;
        }
    }

    private static string ToLaunchUri(Uri uri)
    {
        return uri.Scheme is "http" or "https" ? uri.AbsoluteUri : uri.ToString();
    }
}
