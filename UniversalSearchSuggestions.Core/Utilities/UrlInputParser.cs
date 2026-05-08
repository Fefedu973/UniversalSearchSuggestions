using System.Net;

namespace UniversalSearchSuggestions.Core.Utilities;

public static class UrlInputParser
{
    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        Uri.UriSchemeHttp,
        Uri.UriSchemeHttps,
        Uri.UriSchemeFtp,
        Uri.UriSchemeMailto,
    };

    public static bool TryParse(string input, out Uri uri)
    {
        uri = null!;

        var value = input.Trim();
        if (value.Length == 0 || value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) && AllowedSchemes.Contains(absolute.Scheme))
        {
            uri = NormalizeHttpUri(absolute);
            return true;
        }

        if (!LooksLikeHostOrLocalUrl(value))
        {
            return false;
        }

        if (!Uri.TryCreate("https://" + value, UriKind.Absolute, out var normalized))
        {
            return false;
        }

        uri = NormalizeHttpUri(normalized);
        return true;
    }

    public static string DisplayHost(Uri uri)
    {
        if (!string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.Host;
        }

        return uri.ToString();
    }

    private static Uri NormalizeHttpUri(Uri uri)
    {
        if (uri.Scheme is not ("http" or "https"))
        {
            return uri;
        }

        var builder = new UriBuilder(uri)
        {
            Host = uri.Host.ToLowerInvariant(),
        };

        return builder.Uri;
    }

    private static bool LooksLikeHostOrLocalUrl(string value)
    {
        var hostPart = value.Split(['/', '?', '#'], 2, StringSplitOptions.None)[0];
        var hostWithoutPort = hostPart.Split(':', 2)[0];

        if (hostWithoutPort.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IPAddress.TryParse(hostWithoutPort, out _) &&
            (hostWithoutPort.Contains(':', StringComparison.Ordinal) || hostWithoutPort.Count(static c => c == '.') == 3))
        {
            return true;
        }

        if (!hostWithoutPort.Contains('.', StringComparison.Ordinal))
        {
            return false;
        }

        if (!hostWithoutPort.Any(char.IsLetter))
        {
            return false;
        }

        var parts = hostWithoutPort.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && parts[^1].Length >= 2;
    }
}
