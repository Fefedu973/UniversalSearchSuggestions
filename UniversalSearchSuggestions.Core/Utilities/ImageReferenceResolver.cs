using System.Security.Cryptography;

namespace UniversalSearchSuggestions.Core.Utilities;

public static class ImageReferenceResolver
{
    public static string? Resolve(string? imageReference, string cacheDirectory, bool decodeDataImages)
    {
        if (string.IsNullOrWhiteSpace(imageReference))
        {
            return null;
        }

        if (Uri.TryCreate(imageReference, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https" or "file")
        {
            return imageReference;
        }

        if (!decodeDataImages)
        {
            return null;
        }

        return TryDecodeDataImage(imageReference, cacheDirectory);
    }

    private static string? TryDecodeDataImage(string value, string cacheDirectory)
    {
        var comma = value.IndexOf(',', StringComparison.Ordinal);
        if (value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) && comma > 0)
        {
            var metadata = value[..comma];
            if (!metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var extension = metadata.Contains("jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpg" : ".png";
            return SaveBase64(value[(comma + 1)..], cacheDirectory, extension);
        }

        return LooksLikeRawBase64(value) ? SaveBase64(value, cacheDirectory, ".png") : null;
    }

    private static string? SaveBase64(string base64, string cacheDirectory, string extension)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64);
            if (bytes.Length == 0)
            {
                return null;
            }

            Directory.CreateDirectory(cacheDirectory);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            var path = Path.Combine(cacheDirectory, hash + extension);
            if (!File.Exists(path))
            {
                File.WriteAllBytes(path, bytes);
            }

            return new Uri(path).AbsoluteUri;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool LooksLikeRawBase64(string value)
    {
        if (value.Length < 32 || value.Length % 4 != 0)
        {
            return false;
        }

        return value.All(static c => char.IsLetterOrDigit(c) || c is '+' or '/' or '=');
    }
}
