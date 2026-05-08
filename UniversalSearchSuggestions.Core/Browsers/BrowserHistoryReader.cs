using Microsoft.Data.Sqlite;
using UniversalSearchSuggestions.Core.Resources;
using UniversalSearchSuggestions.Core.Search;

namespace UniversalSearchSuggestions.Core.Browsers;

public static class BrowserHistoryReader
{
    public static IReadOnlyList<SearchSuggestion> ReadChromiumHistory(
        string historyFile,
        string browserName,
        string query,
        int maxResults)
    {
        var parsedQuery = LocalSearchQueryParser.Parse(query);
        return ReadChromiumHistoryEntries(historyFile, browserName, Math.Max(maxResults * 20, 200))
            .Where(entry => LocalSearchQueryParser.Matches(entry.SearchableText, parsedQuery))
            .Select(entry => ToSuggestion(entry, query, ScoreHistoryEntry(entry, parsedQuery)))
            .OrderByDescending(static item => item.Score)
            .Take(maxResults)
            .ToArray();
    }

    public static IReadOnlyList<BrowserLocalEntry> ReadChromiumHistoryEntries(
        string historyFile,
        string browserName,
        int maxEntries)
    {
        const string sql = """
            SELECT url, title, visit_count, typed_count, last_visit_time
            FROM urls
            WHERE last_visit_time > 0
            ORDER BY typed_count DESC, visit_count DESC, last_visit_time DESC
            LIMIT $limit
            """;

        return ReadHistoryDatabase(historyFile, browserName, maxEntries, sql, static reader =>
        {
            var url = reader.GetString(0);
            var title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var visitCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
            var typedCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
            var lastVisitTime = reader.IsDBNull(4) ? 0 : reader.GetInt64(4);
            return (url, title, visitCount, typedCount, FromChromiumTime(lastVisitTime));
        });
    }

    public static IReadOnlyList<SearchSuggestion> ReadFirefoxHistory(
        string placesFile,
        string browserName,
        string query,
        int maxResults)
    {
        var parsedQuery = LocalSearchQueryParser.Parse(query);
        return ReadFirefoxHistoryEntries(placesFile, browserName, Math.Max(maxResults * 20, 200))
            .Where(entry => LocalSearchQueryParser.Matches(entry.SearchableText, parsedQuery))
            .Select(entry => ToSuggestion(entry, query, ScoreHistoryEntry(entry, parsedQuery)))
            .OrderByDescending(static item => item.Score)
            .Take(maxResults)
            .ToArray();
    }

    public static IReadOnlyList<BrowserLocalEntry> ReadFirefoxHistoryEntries(
        string placesFile,
        string browserName,
        int maxEntries)
    {
        const string sql = """
            SELECT url, title, visit_count, last_visit_date
            FROM moz_places
            WHERE last_visit_date IS NOT NULL AND last_visit_date > 0
            ORDER BY frecency DESC, visit_count DESC, last_visit_date DESC
            LIMIT $limit
            """;

        return ReadHistoryDatabase(placesFile, browserName, maxEntries, sql, static reader =>
        {
            var url = reader.GetString(0);
            var title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var visitCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
            var lastVisitDate = reader.IsDBNull(3) ? 0 : reader.GetInt64(3);
            return (url, title, visitCount, 0, FromUnixMicroseconds(lastVisitDate));
        });
    }

    public static IReadOnlyList<BrowserLocalEntry> ReadFirefoxBookmarkEntries(
        string placesFile,
        string browserName,
        int maxEntries)
    {
        const string sql = """
            SELECT COALESCE(NULLIF(b.title, ''), p.title), p.url, p.visit_count, p.last_visit_date
            FROM moz_bookmarks b
            JOIN moz_places p ON b.fk = p.id
            WHERE b.type = 1 AND p.url IS NOT NULL
            ORDER BY b.lastModified DESC, b.dateAdded DESC
            LIMIT $limit
            """;

        return ReadHistoryDatabase(placesFile, browserName, maxEntries, sql, static reader =>
        {
            var title = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var url = reader.GetString(1);
            var visitCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
            var lastVisitDate = reader.IsDBNull(3) ? 0 : reader.GetInt64(3);
            return (url, title, visitCount, 0, FromUnixMicroseconds(lastVisitDate));
        }, SuggestionSourceKind.BrowserBookmark);
    }

    private static List<BrowserLocalEntry> ReadHistoryDatabase(
        string databaseFile,
        string browserName,
        int maxEntries,
        string sql,
        Func<SqliteDataReader, (string Url, string Title, int VisitCount, int TypedCount, DateTimeOffset? LastVisited)> mapRow,
        SuggestionSourceKind sourceKind = SuggestionSourceKind.BrowserHistory)
    {
        if (!File.Exists(databaseFile))
        {
            return [];
        }

        var tempFile = Path.Combine(Path.GetTempPath(), $"uss-history-{Guid.NewGuid():N}.sqlite");
        try
        {
            File.Copy(databaseFile, tempFile, overwrite: true);
            using var connection = new SqliteConnection($"Data Source={tempFile};Mode=ReadOnly");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$limit", maxEntries);

            using var reader = command.ExecuteReader();
            var results = new List<BrowserLocalEntry>();
            while (reader.Read())
            {
                var row = mapRow(reader);
                if (!Uri.TryCreate(row.Url, UriKind.Absolute, out var target))
                {
                    continue;
                }

                results.Add(new BrowserLocalEntry(
                    string.IsNullOrWhiteSpace(row.Title) ? target.Host : row.Title,
                    target,
                    browserName,
                    sourceKind,
                    row.VisitCount,
                    row.TypedCount,
                    row.LastVisited));
            }

            return results;
        }
        catch (SqliteException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
        finally
        {
            TryDelete(tempFile);
        }
    }

    private static SearchSuggestion ToSuggestion(BrowserLocalEntry entry, string query, double score)
    {
        return new SearchSuggestion
        {
            Title = entry.Title,
            Query = query,
            TargetUri = entry.TargetUri,
            Engine = SearchEngineKind.Custom,
            SourceKind = SuggestionSourceKind.BrowserHistory,
            Description = entry.TargetUri.ToString(),
            Section = Strings.SectionHistory,
            TextToSuggest = entry.TargetUri.Host,
            BrowserName = entry.BrowserName,
            Score = score,
            IsNavigation = true,
        };
    }

    internal static double ScoreHistoryEntry(BrowserLocalEntry entry, ParsedLocalSearchQuery query)
    {
        var matchScore = BrowserBookmarkReader.ScoreLocalMatch(entry.Title, entry.TargetUri.ToString(), query);
        if (matchScore <= 0)
        {
            return 0;
        }

        var usageScore = Math.Min(entry.TypedCount * 5 + entry.VisitCount, 35);
        var recencyScore = entry.LastVisited is null
            ? 0
            : Math.Max(0, 18 - (DateTimeOffset.UtcNow - entry.LastVisited.Value).TotalDays / 14);
        return 35 + matchScore * 0.35 + usageScore + recencyScore;
    }

    private static DateTimeOffset? FromChromiumTime(long microsecondsSince1601)
    {
        if (microsecondsSince1601 <= 0)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromFileTime(microsecondsSince1601 * 10).ToUniversalTime();
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static DateTimeOffset? FromUnixMicroseconds(long microseconds)
    {
        if (microseconds <= 0)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(microseconds / 1000).ToUniversalTime();
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
