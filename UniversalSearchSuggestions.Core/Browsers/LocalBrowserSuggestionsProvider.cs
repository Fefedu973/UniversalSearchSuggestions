using System.Collections.Concurrent;
using System.Text.Json;
using UniversalSearchSuggestions.Core.Search;

namespace UniversalSearchSuggestions.Core.Browsers;

public static class LocalBrowserSuggestionsProvider
{
    private const int MinHistoryQueryLength = 3;
    private const int MaxHistoryIndexEntriesPerProfile = 5000;
    private const int MaxFirefoxBookmarkEntriesPerProfile = 5000;
    private static readonly TimeSpan InitialIndexWait = TimeSpan.FromMilliseconds(160);
    private static readonly TimeSpan RefreshCooldown = TimeSpan.FromMinutes(2);
    private static readonly ConcurrentDictionary<string, BrowserIndexCache> Indexes = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<IReadOnlyList<SearchSuggestion>> SearchAsync(
        string query,
        SearchPreferences preferences,
        CancellationToken cancellationToken)
    {
        if ((!preferences.IncludeBrowserBookmarks && !preferences.IncludeBrowserHistory) ||
            preferences.MaxLocalResults <= 0)
        {
            return [];
        }

        var parsedQuery = LocalSearchQueryParser.Parse(query);
        if (parsedQuery.IsEmpty)
        {
            return [];
        }

        var sources = LocalSourceKindsExtensions.FromPreferences(preferences);
        var caches = ResolveLocalBrowsers(preferences)
            .Select(browser => EnsureIndex(browser, sources))
            .ToArray();

        if (caches.Any(static cache => cache.Index is null) &&
            caches.Any(static cache => cache.RefreshTask is { IsCompleted: false }))
        {
            await WaitBrieflyForFirstIndexAsync(caches, cancellationToken).ConfigureAwait(false);
        }

        return caches
            .Select(static cache => cache.Index)
            .OfType<BrowserIndex>()
            .SelectMany(index => SearchIndex(index, parsedQuery, query, preferences))
            .GroupBy(static item => item.TargetUri.ToString(), StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.OrderByDescending(item => item.Score).First())
            .OrderByDescending(static item => item.Score)
            .Take(preferences.MaxLocalResults)
            .ToArray();
    }

    private static async Task WaitBrieflyForFirstIndexAsync(
        IReadOnlyList<BrowserIndexCache> caches,
        CancellationToken cancellationToken)
    {
        var activeTasks = caches
            .Select(static cache => cache.RefreshTask)
            .OfType<Task<BrowserIndex>>()
            .Where(static task => !task.IsCompleted)
            .ToArray();
        if (activeTasks.Length == 0)
        {
            return;
        }

        var delayTask = Task.Delay(InitialIndexWait, cancellationToken);
        await Task.WhenAny(Task.WhenAll(activeTasks), delayTask).ConfigureAwait(false);
    }

    private static BrowserIndexCache EnsureIndex(BrowserTarget browser, LocalSourceKinds sources)
    {
        var files = FindSourceFiles(browser, sources).ToArray();
        var signature = BrowserIndexSignature.FromFiles(files);
        var cacheKey = $"{browser.Id}|{sources}|{signature.FileSetKey}";
        var cache = Indexes.GetOrAdd(cacheKey, static _ => new BrowserIndexCache());

        lock (cache.Gate)
        {
            var needsRefresh = cache.Index is null ||
                cache.Index.Signature != signature ||
                DateTimeOffset.UtcNow - cache.RefreshStartedAt > RefreshCooldown;
            if (needsRefresh && cache.RefreshTask is not { IsCompleted: false })
            {
                cache.RefreshStartedAt = DateTimeOffset.UtcNow;
                cache.RefreshTask = Task.Run(() => BuildIndex(browser, files, signature));
                _ = cache.RefreshTask.ContinueWith(
                    task =>
                    {
                        if (task.Status == TaskStatus.RanToCompletion)
                        {
                            lock (cache.Gate)
                            {
                                cache.Index = task.Result;
                            }
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }

        return cache;
    }

    private static BrowserIndex BuildIndex(
        BrowserTarget browser,
        IReadOnlyList<BrowserSourceFile> files,
        BrowserIndexSignature signature)
    {
        var entries = new List<BrowserLocalEntry>();
        foreach (var file in files)
        {
            entries.AddRange(file.Kind switch
            {
                BrowserSourceFileKind.ChromiumBookmarks => ReadChromiumBookmarks(file.Path, browser.DisplayName),
                BrowserSourceFileKind.ChromiumHistory => BrowserHistoryReader.ReadChromiumHistoryEntries(
                    file.Path,
                    browser.DisplayName,
                    MaxHistoryIndexEntriesPerProfile),
                BrowserSourceFileKind.FirefoxPlacesBookmarks => BrowserHistoryReader.ReadFirefoxBookmarkEntries(
                    file.Path,
                    browser.DisplayName,
                    MaxFirefoxBookmarkEntriesPerProfile),
                BrowserSourceFileKind.FirefoxPlacesHistory => BrowserHistoryReader.ReadFirefoxHistoryEntries(
                    file.Path,
                    browser.DisplayName,
                    MaxHistoryIndexEntriesPerProfile),
                _ => [],
            });
        }

        var uniqueEntries = entries
            .Where(static entry => entry.TargetUri.Scheme is "http" or "https")
            .GroupBy(static entry => $"{entry.SourceKind}:{entry.TargetUri}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group
                .OrderByDescending(entry => entry.TypedCount)
                .ThenByDescending(entry => entry.VisitCount)
                .ThenByDescending(entry => entry.LastVisited)
                .First())
            .ToArray();

        return new BrowserIndex(browser.Id, signature, uniqueEntries);
    }

    private static BrowserLocalEntry[] ReadChromiumBookmarks(string bookmarksFile, string browserName)
    {
        try
        {
            return BrowserBookmarkReader.ReadChromiumBookmarkEntries(bookmarksFile, browserName)
                .Select(entry => new BrowserLocalEntry(
                    entry.Title,
                    entry.TargetUri,
                    entry.BrowserName,
                    SuggestionSourceKind.BrowserBookmark))
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IEnumerable<SearchSuggestion> SearchIndex(
        BrowserIndex index,
        ParsedLocalSearchQuery parsedQuery,
        string rawQuery,
        SearchPreferences preferences)
    {
        var includeHistory = preferences.IncludeBrowserHistory && rawQuery.Trim().Length >= MinHistoryQueryLength;
        foreach (var entry in index.Entries)
        {
            if (entry.SourceKind == SuggestionSourceKind.BrowserBookmark && !preferences.IncludeBrowserBookmarks)
            {
                continue;
            }

            if (entry.SourceKind == SuggestionSourceKind.BrowserHistory && !includeHistory)
            {
                continue;
            }

            if (!LocalSearchQueryParser.Matches(entry.SearchableText, parsedQuery))
            {
                continue;
            }

            var score = entry.SourceKind == SuggestionSourceKind.BrowserBookmark
                ? 90 + BrowserBookmarkReader.ScoreLocalMatch(entry.Title, entry.TargetUri.ToString(), parsedQuery) * 0.2
                : BrowserHistoryReader.ScoreHistoryEntry(entry, parsedQuery);
            if (score <= 0)
            {
                continue;
            }

            yield return ToSuggestion(entry, rawQuery, score);
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
            SourceKind = entry.SourceKind,
            Description = entry.TargetUri.ToString(),
            Section = entry.SourceKind == SuggestionSourceKind.BrowserBookmark ? "Favoris" : "Historique",
            TextToSuggest = entry.TargetUri.Host,
            BrowserName = entry.BrowserName,
            Score = score,
            IsNavigation = true,
        };
    }

    private static BrowserTarget[] ResolveLocalBrowsers(SearchPreferences preferences)
    {
        var installedBrowsers = BrowserInstallDetector.DetectInstalledBrowsers();
        var concreteBrowsers = installedBrowsers
            .Where(static browser => browser.Kind is not (BrowserKind.Default or BrowserKind.Custom))
            .ToList();

        var requestedId = preferences.LocalBrowserId.Equals("same", StringComparison.OrdinalIgnoreCase)
            ? preferences.BrowserId
            : preferences.LocalBrowserId;

        if (!requestedId.Equals("default", StringComparison.OrdinalIgnoreCase) &&
            !requestedId.Equals("custom", StringComparison.OrdinalIgnoreCase))
        {
            var exactMatch = concreteBrowsers.FirstOrDefault(browser =>
                browser.Id.Equals(requestedId, StringComparison.OrdinalIgnoreCase) ||
                browser.DisplayName.Equals(requestedId, StringComparison.OrdinalIgnoreCase));
            if (exactMatch is not null)
            {
                return [exactMatch];
            }
        }

        var defaultBrowserKind = BrowserInstallDetector.DetectDefaultBrowserKind();
        if (defaultBrowserKind is not null)
        {
            var defaultBrowser = concreteBrowsers.FirstOrDefault(browser => browser.Kind == defaultBrowserKind.Value);
            if (defaultBrowser is not null)
            {
                return [defaultBrowser];
            }
        }

        return concreteBrowsers.Take(1).ToArray();
    }

    private static IEnumerable<BrowserSourceFile> FindSourceFiles(BrowserTarget browser, LocalSourceKinds sources)
    {
        if (string.IsNullOrWhiteSpace(browser.UserDataPath) || !Directory.Exists(browser.UserDataPath))
        {
            yield break;
        }

        if (browser.Kind == BrowserKind.Firefox)
        {
            if (sources.HasFlag(LocalSourceKinds.Bookmarks) || sources.HasFlag(LocalSourceKinds.History))
            {
                foreach (var profileDirectory in Directory.EnumerateDirectories(browser.UserDataPath, "*", SearchOption.TopDirectoryOnly))
                {
                    var placesFile = Path.Combine(profileDirectory, "places.sqlite");
                    if (!File.Exists(placesFile))
                    {
                        continue;
                    }

                    if (sources.HasFlag(LocalSourceKinds.Bookmarks))
                    {
                        yield return new BrowserSourceFile(placesFile, BrowserSourceFileKind.FirefoxPlacesBookmarks);
                    }

                    if (sources.HasFlag(LocalSourceKinds.History))
                    {
                        yield return new BrowserSourceFile(placesFile, BrowserSourceFileKind.FirefoxPlacesHistory);
                    }
                }
            }

            yield break;
        }

        foreach (var profileDirectory in EnumerateChromiumProfileDirectories(browser.UserDataPath))
        {
            if (sources.HasFlag(LocalSourceKinds.Bookmarks))
            {
                var bookmarksFile = Path.Combine(profileDirectory, "Bookmarks");
                if (File.Exists(bookmarksFile))
                {
                    yield return new BrowserSourceFile(bookmarksFile, BrowserSourceFileKind.ChromiumBookmarks);
                }
            }

            if (sources.HasFlag(LocalSourceKinds.History))
            {
                var historyFile = Path.Combine(profileDirectory, "History");
                if (File.Exists(historyFile))
                {
                    yield return new BrowserSourceFile(historyFile, BrowserSourceFileKind.ChromiumHistory);
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateChromiumProfileDirectories(string userDataPath)
    {
        var defaultDirectory = Path.Combine(userDataPath, "Default");
        if (Directory.Exists(defaultDirectory))
        {
            yield return defaultDirectory;
        }

        foreach (var directory in Directory.EnumerateDirectories(userDataPath, "Profile *", SearchOption.TopDirectoryOnly))
        {
            yield return directory;
        }
    }

    private sealed record BrowserSourceFile(string Path, BrowserSourceFileKind Kind);

    private sealed record BrowserIndex(string BrowserId, BrowserIndexSignature Signature, IReadOnlyList<BrowserLocalEntry> Entries);

    private sealed class BrowserIndexCache
    {
        public object Gate { get; } = new();

        public BrowserIndex? Index { get; set; }

        public Task<BrowserIndex>? RefreshTask { get; set; }

        public DateTimeOffset RefreshStartedAt { get; set; }
    }

    private sealed record BrowserIndexSignature(string FileSetKey, string LastWriteKey)
    {
        public static BrowserIndexSignature FromFiles(IReadOnlyList<BrowserSourceFile> files)
        {
            var fileSetKey = string.Join(
                ';',
                files
                    .Select(file => $"{file.Kind}:{Path.GetFullPath(file.Path)}")
                    .Order(StringComparer.OrdinalIgnoreCase));
            var lastWriteKey = string.Join(
                ';',
                files
                    .Select(file => $"{file.Kind}:{Path.GetFullPath(file.Path)}:{GetLastWriteTicks(file.Path)}")
                    .Order(StringComparer.OrdinalIgnoreCase));
            return new BrowserIndexSignature(fileSetKey, lastWriteKey);
        }

        private static long GetLastWriteTicks(string path)
        {
            try
            {
                return File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0;
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
        }
    }

    [Flags]
    private enum LocalSourceKinds
    {
        None = 0,
        Bookmarks = 1,
        History = 2,
    }

    private enum BrowserSourceFileKind
    {
        ChromiumBookmarks,
        ChromiumHistory,
        FirefoxPlacesBookmarks,
        FirefoxPlacesHistory,
    }

    private static class LocalSourceKindsExtensions
    {
        public static LocalSourceKinds FromPreferences(SearchPreferences preferences)
        {
            var sources = LocalSourceKinds.None;
            if (preferences.IncludeBrowserBookmarks)
            {
                sources |= LocalSourceKinds.Bookmarks;
            }

            if (preferences.IncludeBrowserHistory)
            {
                sources |= LocalSourceKinds.History;
            }

            return sources;
        }
    }
}
