using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using UniversalSearchSuggestions.Core.Resources;
using UniversalSearchSuggestions.Core.Search;
using UniversalSearchSuggestions.Icons;

namespace UniversalSearchSuggestions.Pages;

internal static class SuggestionTagBuilder
{
    private static readonly OptionalColor BookmarkColor = ColorHelpers.FromRgb(0x1F, 0x6F, 0xEB);
    private static readonly OptionalColor HistoryColor = ColorHelpers.FromRgb(0x8B, 0x5C, 0xF6);
    private static readonly OptionalColor UrlColor = ColorHelpers.FromRgb(0x10, 0xB9, 0x81);
    private static readonly OptionalColor AnswerColor = ColorHelpers.FromRgb(0xF5, 0x9E, 0x0B);
    private static readonly OptionalColor RecentColor = ColorHelpers.FromRgb(0x6B, 0x72, 0x80);
    private static readonly OptionalColor TrendingColor = ColorHelpers.FromRgb(0xEC, 0x4B, 0x99);
    private static readonly OptionalColor WebColor = ColorHelpers.FromRgb(0x06, 0xB6, 0xD4);
    private static readonly OptionalColor LightForeground = ColorHelpers.FromRgb(0xFF, 0xFF, 0xFF);

    public static ITag[] BuildTags(SearchSuggestion suggestion)
    {
        return suggestion.SourceKind switch
        {
            SuggestionSourceKind.DirectUrl => [BuildUrlTag()],
            SuggestionSourceKind.BrowserBookmark => [BuildBookmarkTag(suggestion.BrowserName)],
            SuggestionSourceKind.BrowserHistory => [BuildHistoryTag(suggestion.BrowserName)],
            SuggestionSourceKind.SearchAnswer => [BuildAnswerTag(suggestion)],
            SuggestionSourceKind.SearchEngine => BuildSearchEngineTags(suggestion),
            _ => [],
        };
    }

    private static Tag BuildUrlTag()
    {
        return new Tag(Strings.TagUrl)
        {
            Icon = AppIcons.Link,
            Background = UrlColor,
            Foreground = LightForeground,
            ToolTip = Strings.SubtitleOpenUrlDirectly,
        };
    }

    private static Tag BuildBookmarkTag(string? browserName)
    {
        return new Tag(Strings.TagBookmark)
        {
            Icon = AppIcons.Bookmark,
            Background = BookmarkColor,
            Foreground = LightForeground,
            ToolTip = string.IsNullOrWhiteSpace(browserName)
                ? Strings.TagBookmark
                : Strings.SubtitleLocalBookmark(browserName!),
        };
    }

    private static Tag BuildHistoryTag(string? browserName)
    {
        return new Tag(Strings.TagHistory)
        {
            Icon = AppIcons.History,
            Background = HistoryColor,
            Foreground = LightForeground,
            ToolTip = string.IsNullOrWhiteSpace(browserName)
                ? Strings.TagHistory
                : Strings.SubtitleLocalHistory(browserName!),
        };
    }

    private static Tag BuildAnswerTag(SearchSuggestion suggestion)
    {
        var (text, icon) = ResolveAnswerLabel(suggestion);
        return new Tag(text)
        {
            Icon = icon,
            Background = AnswerColor,
            Foreground = LightForeground,
            ToolTip = Strings.SourceGoogleOmnibox,
        };
    }

    private static ITag[] BuildSearchEngineTags(SearchSuggestion suggestion)
    {
        if (IsRecentSearch(suggestion))
        {
            return
            [
                new Tag(Strings.TagRecent)
                {
                    Icon = AppIcons.Time,
                    Background = RecentColor,
                    Foreground = LightForeground,
                    ToolTip = Strings.SubtitleRecentSearch,
                },
            ];
        }

        if (IsTrendingSuggestion(suggestion))
        {
            return
            [
                new Tag(Strings.TagTrending)
                {
                    Icon = AppIcons.Trending,
                    Background = TrendingColor,
                    Foreground = LightForeground,
                    ToolTip = Strings.SectionGoogleDefaultSuggestions,
                },
            ];
        }

        if (suggestion.IsCurrentQueryAction)
        {
            return
            [
                new Tag(Strings.TagWeb)
                {
                    Icon = AppIcons.Globe,
                    Background = WebColor,
                    Foreground = LightForeground,
                    ToolTip = Strings.TagWeb,
                },
            ];
        }

        return [];
    }

    private static bool IsRecentSearch(SearchSuggestion suggestion)
    {
        return string.Equals(suggestion.Section, Strings.SectionRecentSearches, StringComparison.Ordinal) ||
            string.Equals(suggestion.Description, Strings.SubtitleRecentSearch, StringComparison.Ordinal) ||
            string.Equals(suggestion.IconHint, "time", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTrendingSuggestion(SearchSuggestion suggestion)
    {
        return string.Equals(suggestion.Section, Strings.SectionGoogleDefaultSuggestions, StringComparison.Ordinal) ||
            string.Equals(suggestion.Description, Strings.SubtitleDefaultSuggestion, StringComparison.Ordinal);
    }

    private static (string Text, IconInfo Icon) ResolveAnswerLabel(SearchSuggestion suggestion)
    {
        if (suggestion.IconHint is { } hint)
        {
            if (hint.Equals("calculator", StringComparison.OrdinalIgnoreCase))
            {
                return (Strings.AnswerTypeCalculator, AppIcons.Calculator);
            }

            if (hint.Equals("translate", StringComparison.OrdinalIgnoreCase))
            {
                return (Strings.AnswerTypeTranslation, AppIcons.Translate);
            }

            if (hint.Equals("dictionary", StringComparison.OrdinalIgnoreCase))
            {
                return (Strings.AnswerTypeDictionary, AppIcons.Dictionary);
            }

            if (hint.StartsWith("finance", StringComparison.OrdinalIgnoreCase))
            {
                return (Strings.AnswerTypeFinance, AppIcons.FinanceUp);
            }

            if (hint.Equals("sports", StringComparison.OrdinalIgnoreCase))
            {
                return (Strings.AnswerTypeSports, AppIcons.Sports);
            }

            if (hint.StartsWith("weather", StringComparison.OrdinalIgnoreCase))
            {
                return (Strings.AnswerTypeWeather, AppIcons.WeatherSunny);
            }

            if (hint.Equals("currency", StringComparison.OrdinalIgnoreCase))
            {
                return (Strings.AnswerTypeCurrency, AppIcons.Currency);
            }

            if (hint.Equals("time", StringComparison.OrdinalIgnoreCase))
            {
                return (Strings.AnswerTypeLocalTime, AppIcons.Time);
            }

            if (hint.Equals("local", StringComparison.OrdinalIgnoreCase))
            {
                return (Strings.AnswerTypeLocal, AppIcons.Local);
            }

            if (hint.Equals("app", StringComparison.OrdinalIgnoreCase))
            {
                return (Strings.AnswerTypePlayInstall, AppIcons.App);
            }
        }

        return (Strings.TagAnswer, AppIcons.Answer);
    }
}
