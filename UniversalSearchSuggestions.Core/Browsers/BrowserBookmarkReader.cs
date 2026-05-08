using System.Text.Json;
using UniversalSearchSuggestions.Core.Search;

namespace UniversalSearchSuggestions.Core.Browsers;

public static class BrowserBookmarkReader
{
    public sealed record BookmarkEntry(string Title, Uri TargetUri, string BrowserName);

    public static IReadOnlyList<SearchSuggestion> ReadChromiumBookmarks(
        string bookmarksFile,
        string browserName,
        string query,
        int maxResults)
    {
        var parsedQuery = LocalSearchQueryParser.Parse(query);
        return ReadChromiumBookmarkEntries(bookmarksFile, browserName)
            .Where(entry => LocalSearchQueryParser.Matches($"{entry.Title} {entry.TargetUri}", parsedQuery))
            .Select(entry => (Entry: entry, Score: ScoreLocalMatch(entry.Title, entry.TargetUri.ToString(), parsedQuery)))
            .Where(static item => item.Score > 0)
            .OrderByDescending(static item => item.Score)
            .ThenBy(static item => item.Entry.Title, StringComparer.CurrentCultureIgnoreCase)
            .Take(maxResults)
            .Select(item => ToSuggestion(item.Entry, query, item.Score))
            .ToArray();
    }

    public static IReadOnlyList<BookmarkEntry> ReadChromiumBookmarkEntries(
        string bookmarksFile,
        string browserName)
    {
        if (!File.Exists(bookmarksFile))
        {
            return [];
        }

        using var stream = File.OpenRead(bookmarksFile);
        using var document = JsonDocument.Parse(stream);

        if (!document.RootElement.TryGetProperty("roots", out var roots))
        {
            return [];
        }

        var candidates = new List<BookmarkEntry>();
        foreach (var root in roots.EnumerateObject())
        {
            VisitNode(root.Value, browserName, candidates);
        }

        return candidates
            .OrderBy(static item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static void VisitNode(JsonElement node, string browserName, List<BookmarkEntry> candidates)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var type = TryGetString(node, "type");
        if (type.Equals("url", StringComparison.OrdinalIgnoreCase))
        {
            var title = TryGetString(node, "name");
            var url = TryGetString(node, "url");
            if (!Uri.TryCreate(url, UriKind.Absolute, out var target))
            {
                return;
            }

            candidates.Add(new BookmarkEntry(
                string.IsNullOrWhiteSpace(title) ? target.Host : title,
                target,
                browserName));

            return;
        }

        if (!node.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var child in children.EnumerateArray())
        {
            VisitNode(child, browserName, candidates);
        }
    }

    internal static double ScoreLocalMatch(string title, string url, string query)
    {
        return ScoreLocalMatch(title, url, LocalSearchQueryParser.Parse(query));
    }

    internal static double ScoreLocalMatch(string title, string url, ParsedLocalSearchQuery query)
    {
        if (query.IsEmpty)
        {
            return 0;
        }

        var firstTerm = query.IncludeTerms.Count > 0 ? query.IncludeTerms[0] : null;
        if (string.IsNullOrWhiteSpace(firstTerm))
        {
            return 35;
        }

        if (title.Contains(firstTerm, StringComparison.OrdinalIgnoreCase))
        {
            return 85 - Math.Min(title.IndexOf(firstTerm, StringComparison.OrdinalIgnoreCase), 40);
        }

        if (url.Contains(firstTerm, StringComparison.OrdinalIgnoreCase))
        {
            return 70 - Math.Min(url.IndexOf(firstTerm, StringComparison.OrdinalIgnoreCase) / 2, 35);
        }

        return LocalSearchQueryParser.Matches($"{title} {url}", query) ? 40 : 0;
    }

    private static SearchSuggestion ToSuggestion(BookmarkEntry entry, string query, double score)
    {
        return new SearchSuggestion
        {
            Title = entry.Title,
            Query = query,
            TargetUri = entry.TargetUri,
            Engine = SearchEngineKind.Custom,
            SourceKind = SuggestionSourceKind.BrowserBookmark,
            Description = entry.TargetUri.ToString(),
            Section = "Favoris",
            TextToSuggest = entry.TargetUri.Host,
            BrowserName = entry.BrowserName,
            Score = score,
            IsNavigation = true,
        };
    }

    private static string TryGetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }
}
