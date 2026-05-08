using System.Text.Json;

namespace UniversalSearchSuggestions.Core.Browsers;

public static class BrowserProfileDetector
{
    public static IReadOnlyList<BrowserProfile> Detect(BrowserTarget browser)
    {
        if (string.IsNullOrWhiteSpace(browser.UserDataPath) || !Directory.Exists(browser.UserDataPath))
        {
            return [];
        }

        return browser.Kind switch
        {
            BrowserKind.Firefox => DetectFirefoxProfiles(browser.UserDataPath),
            BrowserKind.Chrome or BrowserKind.Edge or BrowserKind.Brave => DetectChromiumProfiles(browser.UserDataPath),
            _ => [],
        };
    }

    private static BrowserProfile[] DetectChromiumProfiles(string userDataPath)
    {
        var localStatePath = Path.Combine(userDataPath, "Local State");
        if (!File.Exists(localStatePath))
        {
            return [];
        }

        try
        {
            using var stream = File.OpenRead(localStatePath);
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("profile", out var profileSection) ||
                !profileSection.TryGetProperty("info_cache", out var infoCache) ||
                infoCache.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var lastUsed = profileSection.TryGetProperty("last_used", out var lastUsedElement) &&
                lastUsedElement.ValueKind == JsonValueKind.String
                    ? lastUsedElement.GetString()
                    : null;

            var profiles = new List<BrowserProfile>();
            foreach (var entry in infoCache.EnumerateObject())
            {
                var directory = entry.Name;
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

                var name = entry.Value.TryGetProperty("name", out var nameElement) &&
                    nameElement.ValueKind == JsonValueKind.String
                        ? nameElement.GetString()
                        : null;

                if (string.IsNullOrWhiteSpace(name))
                {
                    name = directory;
                }

                var isDefault = string.Equals(directory, lastUsed, StringComparison.OrdinalIgnoreCase) ||
                    (lastUsed is null && directory.Equals("Default", StringComparison.OrdinalIgnoreCase));
                profiles.Add(new BrowserProfile(name!, directory, isDefault));
            }

            return profiles
                .OrderByDescending(static profile => profile.IsDefault)
                .ThenBy(static profile => profile.DisplayName, StringComparer.CurrentCultureIgnoreCase)
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

    private static BrowserProfile[] DetectFirefoxProfiles(string profilesRoot)
    {
        var iniPath = Path.Combine(Path.GetDirectoryName(profilesRoot.TrimEnd(Path.DirectorySeparatorChar))!, "profiles.ini");
        if (!File.Exists(iniPath))
        {
            return [];
        }

        try
        {
            var profiles = new List<BrowserProfile>();
            string? currentSection = null;
            string? currentName = null;
            string? currentPath = null;
            var currentIsDefault = false;
            var currentIsRelative = true;

            void Flush()
            {
                if (currentSection is not null && currentSection.StartsWith("Profile", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(currentName))
                {
                    profiles.Add(new BrowserProfile(
                        currentName!,
                        currentIsRelative && !string.IsNullOrWhiteSpace(currentPath)
                            ? currentName!
                            : currentName!,
                        currentIsDefault));
                }

                currentName = null;
                currentPath = null;
                currentIsDefault = false;
                currentIsRelative = true;
            }

            foreach (var rawLine in File.ReadAllLines(iniPath))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
                {
                    continue;
                }

                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    Flush();
                    currentSection = line[1..^1];
                    continue;
                }

                var separator = line.IndexOf('=', StringComparison.Ordinal);
                if (separator <= 0)
                {
                    continue;
                }

                var key = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim();
                switch (key.ToLowerInvariant())
                {
                    case "name":
                        currentName = value;
                        break;
                    case "path":
                        currentPath = value;
                        break;
                    case "default":
                        currentIsDefault = value.Equals("1", StringComparison.Ordinal);
                        break;
                    case "isrelative":
                        currentIsRelative = !value.Equals("0", StringComparison.Ordinal);
                        break;
                }
            }

            Flush();
            return profiles
                .OrderByDescending(static profile => profile.IsDefault)
                .ThenBy(static profile => profile.DisplayName, StringComparer.CurrentCultureIgnoreCase)
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
