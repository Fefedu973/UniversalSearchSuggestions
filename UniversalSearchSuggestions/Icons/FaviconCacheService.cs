using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using UniversalSearchSuggestions.Core.Utilities;

namespace UniversalSearchSuggestions.Icons;

internal sealed partial class FaviconCacheService(HttpClient httpClient, string cacheDirectory) : IDisposable
{
    private readonly ConcurrentDictionary<string, Task> _downloads = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _failed = new(StringComparer.Ordinal);
    private bool _disposed;

    public event EventHandler? FaviconsChanged;

    public string? GetCachedFaviconOrQueue(Uri targetUri)
    {
        var faviconUrl = FaviconResolver.BuildGoogleFaviconUrl(targetUri);
        if (string.IsNullOrWhiteSpace(faviconUrl))
        {
            return null;
        }

        var cachePath = GetCachePath(faviconUrl);
        if (File.Exists(cachePath))
        {
            return cachePath;
        }

        if (_failed.ContainsKey(faviconUrl))
        {
            return null;
        }

        _downloads.GetOrAdd(faviconUrl, _ => DownloadAsync(faviconUrl, cachePath));
        return null;
    }

    private async Task DownloadAsync(string faviconUrl, string cachePath)
    {
        try
        {
            Directory.CreateDirectory(cacheDirectory);
            using var request = new HttpRequestMessage(HttpMethod.Get, faviconUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/png"));

            using var response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _failed.TryAdd(faviconUrl, 0);
                return;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType is not null && !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                _failed.TryAdd(faviconUrl, 0);
                return;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            if (bytes.Length < 16)
            {
                _failed.TryAdd(faviconUrl, 0);
                return;
            }

            var tempPath = $"{cachePath}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllBytesAsync(tempPath, bytes).ConfigureAwait(false);
            File.Move(tempPath, cachePath, overwrite: true);
            if (!_disposed)
            {
                FaviconsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (HttpRequestException)
        {
            _failed.TryAdd(faviconUrl, 0);
        }
        catch (IOException)
        {
            _failed.TryAdd(faviconUrl, 0);
        }
        catch (UnauthorizedAccessException)
        {
            _failed.TryAdd(faviconUrl, 0);
        }
        finally
        {
            _downloads.TryRemove(faviconUrl, out _);
        }
    }

    private string GetCachePath(string faviconUrl)
    {
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(faviconUrl))).ToLowerInvariant();
        return Path.Combine(cacheDirectory, $"{hash}.png");
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
