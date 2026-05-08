namespace UniversalSearchSuggestions.Core.Utilities;

public static class FaviconResolver
{
    public static string? BuildGoogleFaviconUrl(Uri targetUri, int size = 64)
    {
        if (targetUri.Scheme is not ("http" or "https") ||
            string.IsNullOrWhiteSpace(targetUri.Host) ||
            targetUri.IsLoopback)
        {
            return null;
        }

        var boundedSize = Math.Clamp(size, 16, 128);
        var origin = $"{targetUri.Scheme}://{targetUri.IdnHost}/";
        return $"https://www.google.com/s2/favicons?domain_url={Uri.EscapeDataString(origin)}&sz={boundedSize}";
    }
}
