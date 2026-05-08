namespace UniversalSearchSuggestions.Storage;

internal static class RecentSearchStore
{
    private const int MaxEntries = 30;
    private static readonly object SyncRoot = new();
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UniversalSearchSuggestions",
        "recent-searches.txt");

    public static IReadOnlyList<string> Load()
    {
        lock (SyncRoot)
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return [];
                }

                return File.ReadAllLines(FilePath)
                    .Where(static query => !string.IsNullOrWhiteSpace(query))
                    .Take(MaxEntries)
                    .ToArray();
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
    }

    public static void Record(string query)
    {
        var normalized = query.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        lock (SyncRoot)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var entries = Load()
                    .Where(existing => !existing.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                    .Prepend(normalized)
                    .Take(MaxEntries)
                    .ToArray();
                File.WriteAllLines(FilePath, entries);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
