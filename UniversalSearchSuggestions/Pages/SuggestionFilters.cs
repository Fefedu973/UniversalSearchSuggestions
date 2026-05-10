using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using UniversalSearchSuggestions.Core.Resources;
using UniversalSearchSuggestions.Icons;

namespace UniversalSearchSuggestions.Pages;

internal sealed partial class SuggestionFilters : Filters
{
    public SuggestionFilters()
    {
        CurrentFilterId = SuggestionFilterIds.All;
    }

    public override IFilterItem[] GetFilters()
    {
        return
        [
            new Filter
            {
                Id = SuggestionFilterIds.All,
                Name = Strings.FilterAll,
                Icon = AppIcons.Search,
            },
            new Separator(),
            new Filter
            {
                Id = SuggestionFilterIds.Web,
                Name = Strings.FilterWeb,
                Icon = AppIcons.Globe,
            },
            new Filter
            {
                Id = SuggestionFilterIds.Answers,
                Name = Strings.FilterAnswers,
                Icon = AppIcons.Answer,
            },
            new Filter
            {
                Id = SuggestionFilterIds.Local,
                Name = Strings.FilterLocal,
                Icon = AppIcons.Bookmark,
            },
            new Filter
            {
                Id = SuggestionFilterIds.Navigation,
                Name = Strings.FilterNavigation,
                Icon = AppIcons.Link,
            },
        ];
    }
}
