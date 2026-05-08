using Microsoft.CommandPalette.Extensions.Toolkit;
using System.Collections.Concurrent;
using Windows.Storage;
using Windows.Storage.Streams;

namespace UniversalSearchSuggestions.Icons;

internal static class AppIcons
{
    private static readonly ConcurrentDictionary<string, IconInfo> LocalImageIcons = new(StringComparer.OrdinalIgnoreCase);

    public static IconInfo ExtensionLogo { get; } = IconHelpers.FromRelativePaths(
        @"Assets\logo.png",
        @"Assets\logo.png");

    public static IconInfo Search { get; } = new("\uE721");

    public static IconInfo Link { get; } = new("\uE71B");

    public static IconInfo Bookmark { get; } = new("\uE734");

    public static IconInfo History { get; } = new("\uE81C");

    public static IconInfo Calculator { get; } = new("\uE8EF");

    public static IconInfo Translate { get; } = new("\uF2B7");

    public static IconInfo Dictionary { get; } = new("\uE82D");

    public static IconInfo FinanceUp { get; } = new("\uEAFC");

    public static IconInfo FinanceDown { get; } = new("\uEF42");

    public static IconInfo Sports { get; } = new("\uE805");

    public static IconInfo WeatherCloud { get; } = new("\uE753");

    public static IconInfo WeatherSunny { get; } = new("\uE706");

    public static IconInfo Currency { get; } = new("\uE825");

    public static IconInfo Time { get; } = new("\uE823");

    public static IconInfo Local { get; } = new("\uE707");

    public static IconInfo Answer { get; } = new("\uEA80");

    public static IconInfo App { get; } = new("\uECAA");

    public static IconInfo Profile { get; } = new("\uE77B");

    public static IconInfo Incognito { get; } = new("\uE727");

    public static IconInfo Copy { get; } = new("\uE8C8");

    public static IconInfo FromImageReference(string imageReference)
    {
        if (Uri.TryCreate(imageReference, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeFile)
        {
            return FromLocalImagePath(uri.LocalPath);
        }

        if (Path.IsPathRooted(imageReference) && File.Exists(imageReference))
        {
            return FromLocalImagePath(imageReference);
        }

        return new IconInfo(imageReference);
    }

    private static IconInfo FromLocalImagePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return LocalImageIcons.GetOrAdd(fullPath, static localPath =>
        {
            try
            {
                var file = StorageFile.GetFileFromPathAsync(localPath).AsTask().GetAwaiter().GetResult();
                return new IconInfo(new IconData(RandomAccessStreamReference.CreateFromFile(file)));
            }
            catch (FileNotFoundException)
            {
                return new IconInfo(localPath);
            }
            catch (UnauthorizedAccessException)
            {
                return new IconInfo(localPath);
            }
            catch (InvalidOperationException)
            {
                return new IconInfo(localPath);
            }
        });
    }
}