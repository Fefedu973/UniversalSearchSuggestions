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
        BrowserLauncher.Open(_suggestion.TargetUri, browser, _profile, _privateMode);
        return CommandResult.Dismiss();
    }
}
