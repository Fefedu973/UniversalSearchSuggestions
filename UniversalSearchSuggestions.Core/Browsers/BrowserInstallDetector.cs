using Microsoft.Win32;
using System.Runtime.Versioning;

namespace UniversalSearchSuggestions.Core.Browsers;

public static class BrowserInstallDetector
{
    private static readonly Lazy<List<BrowserTarget>> InstalledBrowsers = new(DetectInstalledBrowsersCore);

    public static IReadOnlyList<BrowserTarget> DetectInstalledBrowsers()
    {
        return InstalledBrowsers.Value;
    }

    public static BrowserKind? DetectDefaultBrowserKind()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice");
        var progId = key?.GetValue("ProgId") as string;
        if (string.IsNullOrWhiteSpace(progId))
        {
            return null;
        }

        if (progId.Contains("MSEdge", StringComparison.OrdinalIgnoreCase))
        {
            return BrowserKind.Edge;
        }

        if (progId.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
        {
            return BrowserKind.Chrome;
        }

        if (progId.Contains("Brave", StringComparison.OrdinalIgnoreCase))
        {
            return BrowserKind.Brave;
        }

        if (progId.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
        {
            return BrowserKind.Firefox;
        }

        return null;
    }

    private static List<BrowserTarget> DetectInstalledBrowsersCore()
    {
        var browsers = new List<BrowserTarget>
        {
            new("default", BrowserKind.Default, "Navigateur par défaut", null, null),
        };

        AddIfFound(browsers, "edge", BrowserKind.Edge, "Microsoft Edge", "msedge.exe", KnownUserDataPath(@"Microsoft\Edge\User Data"),
        [
            KnownProgramPath(@"Microsoft\Edge\Application\msedge.exe", preferX86: true),
            KnownLocalAppDataPath(@"Microsoft\Edge\Application\msedge.exe"),
        ]);

        AddIfFound(browsers, "chrome", BrowserKind.Chrome, "Google Chrome", "chrome.exe", KnownUserDataPath(@"Google\Chrome\User Data"),
        [
            KnownProgramPath(@"Google\Chrome\Application\chrome.exe"),
            KnownProgramPath(@"Google\Chrome\Application\chrome.exe", preferX86: true),
            KnownLocalAppDataPath(@"Google\Chrome\Application\chrome.exe"),
        ]);

        AddIfFound(browsers, "brave", BrowserKind.Brave, "Brave", "brave.exe", KnownUserDataPath(@"BraveSoftware\Brave-Browser\User Data"),
        [
            KnownProgramPath(@"BraveSoftware\Brave-Browser\Application\brave.exe"),
            KnownProgramPath(@"BraveSoftware\Brave-Browser\Application\brave.exe", preferX86: true),
            KnownLocalAppDataPath(@"BraveSoftware\Brave-Browser\Application\brave.exe"),
        ]);

        AddIfFound(browsers, "firefox", BrowserKind.Firefox, "Mozilla Firefox", "firefox.exe", KnownRoamingPath(@"Mozilla\Firefox\Profiles"),
        [
            KnownProgramPath(@"Mozilla Firefox\firefox.exe"),
            KnownProgramPath(@"Mozilla Firefox\firefox.exe", preferX86: true),
            KnownLocalAppDataPath(@"Mozilla Firefox\firefox.exe"),
        ]);

        browsers.Add(new("custom", BrowserKind.Custom, "Chemin personnalisé", null, null));
        return browsers;
    }

    public static BrowserTarget Resolve(string browserId, string? customBrowserPath)
    {
        if (browserId.Equals("custom", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(customBrowserPath))
        {
            return new BrowserTarget("custom", BrowserKind.Custom, "Navigateur personnalisé", customBrowserPath, null);
        }

        return InstalledBrowsers.Value.FirstOrDefault(browser => browser.Id.Equals(browserId, StringComparison.OrdinalIgnoreCase)) ??
            InstalledBrowsers.Value[0];
    }

    private static void AddIfFound(
        List<BrowserTarget> browsers,
        string id,
        BrowserKind kind,
        string displayName,
        string appPathExecutableName,
        string? userDataPath,
        IEnumerable<string?> candidatePaths)
    {
        var executablePath = FindAppPath(appPathExecutableName) ??
            candidatePaths.FirstOrDefault(static path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));

        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            browsers.Add(new BrowserTarget(id, kind, displayName, executablePath, userDataPath));
        }
    }

    private static string? FindAppPath(string executableName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        return ReadAppPath(Registry.CurrentUser, executableName) ??
            ReadAppPath(Registry.LocalMachine, executableName);
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadAppPath(RegistryKey root, string executableName)
    {
        using var key = root.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{executableName}");
        var value = key?.GetValue(null) as string;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? KnownProgramPath(string relativePath, bool preferX86 = false)
    {
        var basePath = Environment.GetFolderPath(preferX86
            ? Environment.SpecialFolder.ProgramFilesX86
            : Environment.SpecialFolder.ProgramFiles);
        return string.IsNullOrWhiteSpace(basePath) ? null : Path.Combine(basePath, relativePath);
    }

    private static string? KnownLocalAppDataPath(string relativePath)
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(basePath) ? null : Path.Combine(basePath, relativePath);
    }

    private static string? KnownUserDataPath(string relativePath)
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(basePath) ? null : Path.Combine(basePath, relativePath);
    }

    private static string? KnownRoamingPath(string relativePath)
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return string.IsNullOrWhiteSpace(basePath) ? null : Path.Combine(basePath, relativePath);
    }
}
