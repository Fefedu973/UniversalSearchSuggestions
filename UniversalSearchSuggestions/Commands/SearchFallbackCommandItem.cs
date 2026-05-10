using Microsoft.CommandPalette.Extensions.Toolkit;
using UniversalSearchSuggestions.Core.Resources;
using UniversalSearchSuggestions.Core.Search;
using UniversalSearchSuggestions.Core.Utilities;
using UniversalSearchSuggestions.Icons;
using UniversalSearchSuggestions.Settings;

namespace UniversalSearchSuggestions.Commands;

internal sealed partial class SearchFallbackCommandItem : FallbackCommandItem
{
    private readonly SearchSettingsManager _settingsManager;
    private readonly OpenUrlCommand _openCommand;

    public SearchFallbackCommandItem(SearchSettingsManager settingsManager)
        : this(settingsManager, new OpenUrlCommand(settingsManager.Snapshot()))
    {
    }

    private SearchFallbackCommandItem(SearchSettingsManager settingsManager, OpenUrlCommand openCommand)
        : base(openCommand, displayTitle: Strings.FallbackDefaultTitle, id: "com.fefedu973.universalsearchsuggestions.fallback")
    {
        _settingsManager = settingsManager;
        _openCommand = openCommand;

        Title = string.Empty;
        Subtitle = Strings.FallbackDefaultSubtitle;
        Icon = AppIcons.Globe;
    }

    public override void UpdateQuery(string query)
    {
        var trimmed = query?.Trim() ?? string.Empty;
        var preferences = _settingsManager.Snapshot();

        if (trimmed.Length == 0)
        {
            Title = string.Empty;
            Subtitle = Strings.FallbackDefaultSubtitle;
            _openCommand.SetTarget(null, preferences, isNavigation: false, searchQuery: null);
            return;
        }

        if (UrlInputParser.TryParse(trimmed, out var directUri))
        {
            var displayHost = UrlInputParser.DisplayHost(directUri);
            Title = Strings.FallbackOpenUrlTitle(displayHost);
            Subtitle = Strings.SubtitleOpenUrlDirectly;
            Icon = AppIcons.Link;
            _openCommand.SetTarget(directUri, preferences, isNavigation: true, searchQuery: null);
            return;
        }

        var engineName = SearchEngineCatalog.Get(preferences.PrimaryEngine).DisplayName;
        var target = SuggestionFetchService.BuildPreferredSearchUri(trimmed, preferences);
        Title = Strings.FallbackTitle(trimmed, engineName);
        Subtitle = Strings.SubtitleEngineSuggestion(engineName, trimmed);
        Icon = AppIcons.Globe;
        _openCommand.SetTarget(target, preferences, isNavigation: false, searchQuery: trimmed);
    }
}
