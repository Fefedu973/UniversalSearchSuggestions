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

    public static IconInfo Search { get; } = IconHelpers.FromRelativePaths(
        @"Assets\Search.light.png",
        @"Assets\Search.dark.png");

    public static IconInfo Link { get; } = IconHelpers.FromRelativePaths(
        @"Assets\Link.light.png",
        @"Assets\Link.dark.png");

    public static IconInfo Bookmark { get; } = IconHelpers.FromRelativePaths(
        @"Assets\Bookmark.light.png",
        @"Assets\Bookmark.dark.png");

    public static IconInfo History { get; } = IconHelpers.FromRelativePaths(
        @"Assets\History.light.png",
        @"Assets\History.dark.png");

    public static IconInfo Calculator { get; } = IconHelpers.FromRelativePaths(
        @"Assets\Calculator.light.png",
        @"Assets\Calculator.dark.png");

    public static IconInfo Translate { get; } = IconHelpers.FromRelativePaths(
        @"Assets\Translate.light.png",
        @"Assets\Translate.dark.png");

    public static IconInfo Dictionary { get; } = IconHelpers.FromRelativePaths(
        @"Assets\Dictionary.light.png",
        @"Assets\Dictionary.dark.png");

    public static IconInfo Finance { get; } = IconHelpers.FromRelativePaths(
        @"Assets\Finance.light.png",
        @"Assets\Finance.dark.png");

    public static IconInfo Sports { get; } = IconHelpers.FromRelativePaths(
        @"Assets\Sports.light.png",
        @"Assets\Sports.dark.png");

    public static IconInfo Weather { get; } = IconHelpers.FromRelativePaths(
        @"Assets\Weather.light.png",
        @"Assets\Weather.dark.png");

    public static IconInfo Currency { get; } = IconHelpers.FromRelativePaths(
        @"Assets\Currency.light.png",
        @"Assets\Currency.dark.png");

    public static IconInfo Time { get; } = IconHelpers.FromRelativePaths(
        @"Assets\Time.light.png",
        @"Assets\Time.dark.png");

    public static IconInfo Local { get; } = IconHelpers.FromRelativePaths(
        @"Assets\Local.light.png",
        @"Assets\Local.dark.png");

    public static IconInfo Answer { get; } = IconHelpers.FromRelativePaths(
        @"Assets\Answer.light.png",
        @"Assets\Answer.dark.png");

    public static IconInfo App { get; } = IconHelpers.FromRelativePaths(
        @"Assets\App.light.png",
        @"Assets\App.dark.png");

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
