using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using UniversalSearchSuggestions.Core.Browsers;
using UniversalSearchSuggestions.Core.Resources;
using UniversalSearchSuggestions.Core.Search;
using UniversalSearchSuggestions.Icons;
using UniversalSearchSuggestions.Storage;

namespace UniversalSearchSuggestions.Commands;

internal sealed partial class OpenUrlCommand : InvokableCommand
{
    private Uri? _target;
    private SearchPreferences _preferences;
    private bool _isNavigation;
    private string? _searchQuery;

    public OpenUrlCommand(SearchPreferences preferences)
    {
        _preferences = preferences;
        Name = Strings.CommandSearch;
        Icon = AppIcons.Search;
    }

    public void SetTarget(Uri? target, SearchPreferences preferences, bool isNavigation, string? searchQuery)
    {
        _target = target;
        _preferences = preferences;
        _isNavigation = isNavigation;
        _searchQuery = searchQuery;
        Name = isNavigation ? Strings.CommandOpen : Strings.CommandSearch;
        Icon = isNavigation ? AppIcons.Link : AppIcons.Search;
    }

    public override ICommandResult Invoke()
    {
        if (_target is null)
        {
            return CommandResult.KeepOpen();
        }

        if (!_isNavigation && !string.IsNullOrWhiteSpace(_searchQuery))
        {
            RecentSearchStore.Record(_searchQuery!);
        }

        var browser = BrowserInstallDetector.Resolve(_preferences.BrowserId, _preferences.CustomBrowserPath);
        BrowserLauncher.Open(_target, browser, profile: null, privateMode: false);
        return CommandResult.Dismiss();
    }
}
