using System.Diagnostics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using UniversalSearchSuggestions.Core.Browsers;
using UniversalSearchSuggestions.Core.Resources;
using UniversalSearchSuggestions.Core.Search;
using UniversalSearchSuggestions.Icons;
using UniversalSearchSuggestions.Storage;

namespace UniversalSearchSuggestions.Commands;

internal sealed partial class OpenSearchTargetCommand : InvokableCommand
{
    private readonly SearchSuggestion _suggestion;
    private readonly SearchPreferences _preferences;
    private readonly BrowserTarget? _browserOverride;
    private readonly BrowserProfile? _profile;
    private readonly bool _privateMode;
    private readonly string? _nameOverride;

    public OpenSearchTargetCommand(SearchSuggestion suggestion, SearchPreferences preferences)
        : this(suggestion, preferences, browserOverride: null, profile: null, privateMode: false, nameOverride: null)
    {
    }

    public OpenSearchTargetCommand(
        SearchSuggestion suggestion,
        SearchPreferences preferences,
        BrowserTarget? browserOverride,
        BrowserProfile? profile,
        bool privateMode,
        string? nameOverride = null)
    {
        _suggestion = suggestion;
        _preferences = preferences;
        _browserOverride = browserOverride;
        _profile = profile;
        _privateMode = privateMode;
        _nameOverride = nameOverride;
    }

    public override string Name => _nameOverride ?? (_suggestion.IsNavigation ? Strings.CommandOpen : Strings.CommandSearch);

    public override IconInfo Icon => _suggestion.IsNavigation ? AppIcons.Link : AppIcons.Search;

    public override ICommandResult Invoke()
    {
        if (!_suggestion.IsNavigation &&
            _suggestion.SourceKind is SuggestionSourceKind.SearchEngine or SuggestionSourceKind.SearchAnswer)
        {
            RecentSearchStore.Record(_suggestion.Query);
        }

        var browser = _browserOverride ??
            BrowserInstallDetector.Resolve(_preferences.BrowserId, _preferences.CustomBrowserPath);
        OpenTarget(_suggestion.TargetUri, browser, _profile, _privateMode);
        return CommandResult.Dismiss();
    }

    private static void OpenTarget(Uri target, BrowserTarget browser, BrowserProfile? profile, bool privateMode)
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
