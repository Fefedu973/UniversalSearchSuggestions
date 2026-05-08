using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniversalSearchSuggestions.Core.Resources;
using UniversalSearchSuggestions.Core.Search;

namespace UniversalSearchSuggestions.RichDetails;

internal sealed partial class RichDetailsService(HttpClient httpClient, string? imageCacheDirectory = null) : IDisposable
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AiAnswerDelay = TimeSpan.FromMilliseconds(900);
    private static readonly Uri DefaultAiEndpoint = new("https://oai.endpoints.kepler.ai.cloud.ovh.net/v1/chat/completions");
    private readonly ConcurrentDictionary<string, DetailState> _states = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _imageCache = new(StringComparer.Ordinal);
    private readonly object _cancellationLock = new();
    private readonly string? _imageCacheDirectory = imageCacheDirectory;
    private CancellationTokenSource _loadCts = new();

    public event EventHandler? DetailsChanged;

    public void CancelPendingLoads()
    {
        CancellationTokenSource previous;
        lock (_cancellationLock)
        {
            previous = _loadCts;
            _loadCts = new CancellationTokenSource();
        }

        previous.Cancel();
        previous.Dispose();
    }

    public void Dispose()
    {
        CancellationTokenSource cts;
        lock (_cancellationLock)
        {
            cts = _loadCts;
            _loadCts = new CancellationTokenSource();
        }

        cts.Cancel();
        cts.Dispose();
    }

    public string? GetCachedMarkdownOrQueue(
        SearchSuggestion suggestion,
        SearchPreferences preferences,
        bool allowAiAnswer,
        Action<string?>? onChanged = null)
    {
        if (!ShouldFetch(suggestion, preferences, allowAiAnswer))
        {
            return null;
        }

        var key = BuildKey(suggestion, preferences, allowAiAnswer);
        var options = new DetailOptions(
            EnableRichWebDetails: preferences.EnableRichWebDetails,
            EnableAiAnswerDetails: allowAiAnswer && preferences.EnableAiAnswerDetails,
            EnableAiAnswerDebug: preferences.EnableAiAnswerDebug && allowAiAnswer);
        var state = _states.GetOrAdd(key, _ => new DetailState(DateTimeOffset.UtcNow, options.EnableAiAnswerDebug));
        if (DateTimeOffset.UtcNow - state.CreatedAt > CacheDuration)
        {
            _states.TryRemove(key, out _);
            state = _states.GetOrAdd(key, _ => new DetailState(DateTimeOffset.UtcNow, options.EnableAiAnswerDebug));
        }

        if (onChanged is not null)
        {
            state.AddObserver(onChanged);
        }

        if (state.TryStart())
        {
            _ = FetchAsync(key, state, suggestion.Query, preferences, options, CurrentToken());
        }

        return state.ToMarkdown();
    }

    public bool HasResolvedContent(SearchSuggestion suggestion, SearchPreferences preferences, bool allowAiAnswer)
    {
        if (!ShouldFetch(suggestion, preferences, allowAiAnswer))
        {
            return false;
        }

        var key = BuildKey(suggestion, preferences, allowAiAnswer);
        return _states.TryGetValue(key, out var state) && !string.IsNullOrWhiteSpace(state.ToMarkdown());
    }

    private CancellationToken CurrentToken()
    {
        lock (_cancellationLock)
        {
            return _loadCts.Token;
        }
    }

    private async Task FetchAsync(
        string key,
        DetailState state,
        string query,
        SearchPreferences preferences,
        DetailOptions options,
        CancellationToken cancellationToken)
    {
        if (state.EnableAiDebug)
        {
            state.SetAiStatus(options.EnableAiAnswerDetails ? AiStatus.Delaying : AiStatus.Disabled);
            NotifyStateChanged(state);
        }

        try
        {
            var webTask = options.EnableRichWebDetails
                ? FetchWebDetailsAsync(query, preferences, cancellationToken)
                : Task.FromResult<string?>(null);

            var aiTask = options.EnableAiAnswerDetails
                ? FetchAiDetailsWithDelayAsync(query, preferences, state, cancellationToken)
                : Task.CompletedTask;

            var webMarkdown = await webTask.ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(webMarkdown))
            {
                state.SetWebMarkdown(webMarkdown);
                NotifyStateChanged(state);
            }

            await aiTask.ConfigureAwait(false);
            state.Complete();
            NotifyStateChanged(state);
            state.ClearObservers();
        }
        catch (OperationCanceledException)
        {
            if (state.EnableAiDebug)
            {
                state.SetAiStatus(AiStatus.Cancelled);
            }
            _states.TryRemove(key, out _);
        }
        catch (HttpRequestException ex)
        {
            if (state.EnableAiDebug)
            {
                state.SetAiError(ex.Message);
            }
            state.Complete();
            NotifyStateChanged(state);
            state.ClearObservers();
        }
        catch (JsonException ex)
        {
            if (state.EnableAiDebug)
            {
                state.SetAiError(ex.Message);
            }
            state.Complete();
            NotifyStateChanged(state);
            state.ClearObservers();
        }
        catch (InvalidOperationException ex)
        {
            if (state.EnableAiDebug)
            {
                state.SetAiError(ex.Message);
            }
            state.Complete();
            NotifyStateChanged(state);
            state.ClearObservers();
        }
    }

    private void NotifyStateChanged(DetailState state)
    {
        state.NotifyChanged();
        DetailsChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task<string?> FetchWebDetailsAsync(
        string query,
        SearchPreferences preferences,
        CancellationToken cancellationToken)
    {
        var configured = await FetchConfiguredRichDetailsAsync(query, preferences, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var duckDuckGo = await FetchDuckDuckGoAsync(query, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(duckDuckGo))
        {
            return duckDuckGo;
        }

        return await FetchWikipediaAsync(query, preferences.Language, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> FetchConfiguredRichDetailsAsync(
        string query,
        SearchPreferences preferences,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(preferences.RichDetailsEndpointTemplate))
        {
            return null;
        }

        try
        {
            var endpoint = BuildEndpoint(
                preferences.RichDetailsEndpointTemplate,
                prompt: query,
                query,
                preferences.Language);
            using var response = await httpClient
                .SendAsync(new HttpRequestMessage(HttpMethod.Get, endpoint), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(payload);
            return BuildConfiguredDetailsMarkdown(document.RootElement);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string? BuildConfiguredDetailsMarkdown(JsonElement root)
    {
        var markdown = new StringBuilder();
        AppendKnownSection(markdown, root, "answer_box", Strings.RichDetailsHeaderAnswerSingular);
        AppendKnownSection(markdown, root, "answerBox", Strings.RichDetailsHeaderAnswerSingular);
        AppendKnownSection(markdown, root, "answers", Strings.RichDetailsHeaderAnswerPlural);
        AppendKnownSection(markdown, root, "answer", Strings.RichDetailsHeaderAnswerSingular);
        AppendKnownSection(markdown, root, "infoboxes", Strings.RichDetailsHeaderInfobox);
        AppendKnownSection(markdown, root, "infobox", Strings.RichDetailsHeaderInfobox);
        AppendKnownSection(markdown, root, "knowledge_graph", Strings.RichDetailsHeaderKnowledgeGraph);
        AppendKnownSection(markdown, root, "knowledgeGraph", Strings.RichDetailsHeaderKnowledgeGraph);

        if (markdown.Length == 0)
        {
            return null;
        }

        markdown.Insert(0, $"#### {Strings.RichDetailsHeaderEnrichedResult}\n");
        return markdown.ToString();
    }

    private static void AppendKnownSection(StringBuilder markdown, JsonElement root, string propertyName, string label)
    {
        if (!TryGetProperty(root, propertyName, out var value) || !HasMeaningfulJsonValue(value))
        {
            return;
        }

        if (markdown.Length > 0)
        {
            markdown.AppendLine();
        }

        markdown.Append("**");
        markdown.Append(EscapeMarkdown(label));
        markdown.AppendLine("**");
        AppendJsonSummary(markdown, value);
    }

    private static void AppendJsonSummary(StringBuilder markdown, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                AppendPlainLine(markdown, value.GetString());
                return;

            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                markdown.AppendLine(value.ToString());
                return;

            case JsonValueKind.Array:
                AppendJsonArraySummary(markdown, value);
                return;

            case JsonValueKind.Object:
                AppendJsonObjectSummary(markdown, value);
                return;
        }
    }

    private static void AppendJsonArraySummary(StringBuilder markdown, JsonElement array)
    {
        var count = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (!HasMeaningfulJsonValue(item))
            {
                continue;
            }

            if (item.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                markdown.Append("- ");
                markdown.AppendLine(EscapeMarkdown(Trim(item.ToString(), 300)));
            }
            else
            {
                AppendJsonSummary(markdown, item);
            }

            count++;
            if (count >= 4)
            {
                break;
            }
        }
    }

    private static void AppendJsonObjectSummary(StringBuilder markdown, JsonElement value)
    {
        var wrote = false;
        var title = TryGetAnyString(value, "title", "name", "heading", "label");
        if (!string.IsNullOrWhiteSpace(title))
        {
            markdown.Append("**");
            markdown.Append(EscapeMarkdown(title));
            markdown.AppendLine("**");
            wrote = true;
        }

        foreach (var propertyName in new[] { "answer", "result", "snippet", "description", "content", "abstract", "extract", "definition", "value" })
        {
            var text = TryGetAnyString(value, propertyName);
            if (!string.IsNullOrWhiteSpace(text))
            {
                AppendPlainLine(markdown, text);
                wrote = true;
                break;
            }
        }

        if (TryGetProperty(value, "attributes", out var attributes) && HasMeaningfulJsonValue(attributes))
        {
            AppendAttributes(markdown, attributes);
            wrote = true;
        }

        var sourceUrl = TryGetAnyString(value, "url", "link", "source", "source_url", "sourceUrl");
        if (!string.IsNullOrWhiteSpace(sourceUrl) && Uri.TryCreate(sourceUrl, UriKind.Absolute, out _))
        {
            markdown.AppendLine();
            markdown.Append('[');
            markdown.Append(EscapeMarkdown(Strings.RichDetailsSource));
            markdown.Append("](");
            markdown.Append(sourceUrl);
            markdown.AppendLine(")");
            wrote = true;
        }

        if (!wrote)
        {
            AppendFirstScalarProperties(markdown, value);
        }
    }

    private static void AppendAttributes(StringBuilder markdown, JsonElement attributes)
    {
        if (attributes.ValueKind == JsonValueKind.Object)
        {
            var count = 0;
            foreach (var property in attributes.EnumerateObject())
            {
                if (!HasMeaningfulJsonValue(property.Value))
                {
                    continue;
                }

                markdown.Append("- **");
                markdown.Append(EscapeMarkdown(property.Name));
                markdown.Append("**: ");
                markdown.AppendLine(EscapeMarkdown(Trim(property.Value.ToString(), 200)));
                count++;
                if (count >= 6)
                {
                    break;
                }
            }
        }
        else if (attributes.ValueKind == JsonValueKind.Array)
        {
            AppendJsonArraySummary(markdown, attributes);
        }
    }

    private static void AppendFirstScalarProperties(StringBuilder markdown, JsonElement value)
    {
        var count = 0;
        foreach (var property in value.EnumerateObject())
        {
            if (property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False) ||
                !HasMeaningfulJsonValue(property.Value))
            {
                continue;
            }

            markdown.Append("- **");
            markdown.Append(EscapeMarkdown(property.Name));
            markdown.Append("**: ");
            markdown.AppendLine(EscapeMarkdown(Trim(property.Value.ToString(), 250)));
            count++;
            if (count >= 5)
            {
                break;
            }
        }
    }

    private async Task<string?> FetchDuckDuckGoAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            var uri = new Uri($"https://api.duckduckgo.com/?q={Uri.EscapeDataString(query)}&format=json&no_html=1&skip_disambig=1");
            var payload = await httpClient.GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            var heading = TryGetString(root, "Heading");
            var answer = TryGetString(root, "Answer");
            var answerType = TryGetString(root, "AnswerType");
            var abstractText = TryGetString(root, "AbstractText");
            var definition = TryGetString(root, "Definition");
            var sourceUrl = TryGetString(root, "AbstractURL") ?? TryGetString(root, "DefinitionURL");
            var image = TryGetString(root, "Image");

            if (string.IsNullOrWhiteSpace(answer) &&
                string.IsNullOrWhiteSpace(abstractText) &&
                string.IsNullOrWhiteSpace(definition))
            {
                return null;
            }

            var markdown = new StringBuilder();
            markdown.Append("#### ").AppendLine(Strings.RichDetailsHeaderWebDetails);
            if (!string.IsNullOrWhiteSpace(image) && image.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var cachedImage = await GetOrDownloadImageAsync(image, cancellationToken).ConfigureAwait(false);
                markdown.Append("![");
                markdown.Append(EscapeMarkdown(heading ?? query));
                markdown.Append("](");
                markdown.Append(cachedImage ?? image);
                markdown.AppendLine(")");
                markdown.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(heading))
            {
                markdown.Append("**");
                markdown.Append(EscapeMarkdown(heading));
                markdown.AppendLine("**");
            }

            if (!string.IsNullOrWhiteSpace(answer))
            {
                markdown.AppendLine(EscapeMarkdown(answer));
                if (!string.IsNullOrWhiteSpace(answerType))
                {
                    markdown.AppendLine();
                    markdown.AppendLine(EscapeMarkdown(answerType));
                }
            }
            else if (!string.IsNullOrWhiteSpace(definition))
            {
                markdown.AppendLine(EscapeMarkdown(definition));
            }
            else if (!string.IsNullOrWhiteSpace(abstractText))
            {
                markdown.AppendLine(EscapeMarkdown(Trim(abstractText, 700)));
            }

            if (!string.IsNullOrWhiteSpace(sourceUrl))
            {
                markdown.AppendLine();
                markdown.Append('[');
                markdown.Append(EscapeMarkdown(Strings.RichDetailsSource));
                markdown.Append("](");
                markdown.Append(sourceUrl);
                markdown.AppendLine(")");
            }

            return markdown.ToString();
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<string?> FetchWikipediaAsync(string query, string language, CancellationToken cancellationToken)
    {
        try
        {
            var lang = NormalizeWikipediaLanguage(language);
            var searchUri = new Uri($"https://{lang}.wikipedia.org/w/api.php?action=opensearch&search={Uri.EscapeDataString(query)}&limit=1&namespace=0&format=json");
            var searchPayload = await httpClient.GetStringAsync(searchUri, cancellationToken).ConfigureAwait(false);
            using var searchDocument = JsonDocument.Parse(searchPayload);
            var root = searchDocument.RootElement;
            if (root.ValueKind != JsonValueKind.Array ||
                root.GetArrayLength() < 2 ||
                root[1].ValueKind != JsonValueKind.Array ||
                root[1].GetArrayLength() == 0)
            {
                return null;
            }

            var title = root[1][0].GetString();
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            var summaryUri = new Uri($"https://{lang}.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(title.Replace(' ', '_'))}");
            var summaryPayload = await httpClient.GetStringAsync(summaryUri, cancellationToken).ConfigureAwait(false);
            using var summaryDocument = JsonDocument.Parse(summaryPayload);
            var summary = summaryDocument.RootElement;
            var extract = TryGetString(summary, "extract");
            if (string.IsNullOrWhiteSpace(extract))
            {
                return null;
            }

            var pageUrl = TryGetNestedString(summary, "content_urls", "desktop", "page");
            var thumbnail = TryGetNestedString(summary, "thumbnail", "source");
            var description = TryGetString(summary, "description");

            var markdown = new StringBuilder();
            markdown.Append("#### ").AppendLine(Strings.RichDetailsHeaderWikipedia);
            if (!string.IsNullOrWhiteSpace(thumbnail))
            {
                var cachedThumbnail = await GetOrDownloadImageAsync(thumbnail, cancellationToken).ConfigureAwait(false);
                markdown.Append("![");
                markdown.Append(EscapeMarkdown(title));
                markdown.Append("](");
                markdown.Append(cachedThumbnail ?? thumbnail);
                markdown.AppendLine(")");
                markdown.AppendLine();
            }

            markdown.Append("**");
            markdown.Append(EscapeMarkdown(title));
            markdown.AppendLine("**");
            if (!string.IsNullOrWhiteSpace(description))
            {
                markdown.AppendLine(EscapeMarkdown(description));
                markdown.AppendLine();
            }

            markdown.AppendLine(EscapeMarkdown(Trim(extract, 800)));
            if (!string.IsNullOrWhiteSpace(pageUrl))
            {
                markdown.AppendLine();
                markdown.Append('[');
                markdown.Append(EscapeMarkdown(Strings.RichDetailsReadOnWikipedia));
                markdown.Append("](");
                markdown.Append(pageUrl);
                markdown.AppendLine(")");
            }

            return markdown.ToString();
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task FetchAiDetailsWithDelayAsync(
        string query,
        SearchPreferences preferences,
        DetailState state,
        CancellationToken cancellationToken)
    {
        await Task.Delay(AiAnswerDelay, cancellationToken).ConfigureAwait(false);
        await FetchAiDetailsAsync(query, preferences, state, cancellationToken).ConfigureAwait(false);
    }

    private async Task FetchAiDetailsAsync(
        string query,
        SearchPreferences preferences,
        DetailState state,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(preferences.AiAnswerEndpointTemplate))
        {
            return;
        }

        var prompt = Strings.RichDetailsAiPrompt(ResolveAiPromptLanguage(preferences.Language), query);
        var endpoint = BuildEndpoint(preferences.AiAnswerEndpointTemplate, prompt, query, preferences.Language);
        if (state.EnableAiDebug)
        {
            state.SetAiEndpoint(endpoint.AbsoluteUri, preferences.AiAnswerModel);
            state.SetAiStatus(AiStatus.Requesting);
            NotifyStateChanged(state);
        }

        if (IsOpenAiCompatibleChatEndpoint(endpoint, preferences.AiAnswerEndpointTemplate))
        {
            await FetchOpenAiCompatibleStreamAsync(endpoint, preferences.AiAnswerModel, preferences.AiAnswerApiKey, prompt, state, cancellationToken).ConfigureAwait(false);
            return;
        }

        await FetchPlainTextStreamAsync(endpoint, preferences.AiAnswerApiKey, state, cancellationToken).ConfigureAwait(false);
    }

    private async Task FetchOpenAiCompatibleStreamAsync(
        Uri endpoint,
        string model,
        string apiKey,
        string prompt,
        DetailState state,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Accept.ParseAdd("text/event-stream");
            ApplyAuthorizationHeader(request, apiKey);
            request.Content = new StringContent(
                BuildOpenAiRequestBody(model, prompt),
                Encoding.UTF8,
                "application/json");

            using var response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                if (state.EnableAiDebug)
                {
                    state.SetAiError($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                    NotifyStateChanged(state);
                }
                return;
            }

            if (state.EnableAiDebug)
            {
                state.SetAiStatus(AiStatus.Streaming);
                NotifyStateChanged(state);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream);
            var lastUpdate = DateTimeOffset.UtcNow;
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var data = line[5..].Trim();
                if (data.Equals("[DONE]", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                var chunk = ExtractOpenAiChunk(data);
                if (string.IsNullOrEmpty(chunk))
                {
                    continue;
                }

                state.AppendAiMarkdown(chunk);
                if (DateTimeOffset.UtcNow - lastUpdate > TimeSpan.FromMilliseconds(250))
                {
                    lastUpdate = DateTimeOffset.UtcNow;
                    NotifyStateChanged(state);
                }
            }

            if (state.EnableAiDebug)
            {
                state.SetAiStatus(AiStatus.Done);
            }
            NotifyStateChanged(state);
        }
        catch (HttpRequestException ex)
        {
            if (state.EnableAiDebug)
            {
                state.SetAiError(ex.Message);
                NotifyStateChanged(state);
            }
        }
        catch (JsonException ex)
        {
            if (state.EnableAiDebug)
            {
                state.SetAiError(ex.Message);
                NotifyStateChanged(state);
            }
        }
        catch (InvalidOperationException ex)
        {
            if (state.EnableAiDebug)
            {
                state.SetAiError(ex.Message);
                NotifyStateChanged(state);
            }
        }
    }

    private async Task FetchPlainTextStreamAsync(Uri endpoint, string apiKey, DetailState state, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            ApplyAuthorizationHeader(request, apiKey);
            using var response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                if (state.EnableAiDebug)
                {
                    state.SetAiError($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                    NotifyStateChanged(state);
                }
                return;
            }

            if (state.EnableAiDebug)
            {
                state.SetAiStatus(AiStatus.Streaming);
                NotifyStateChanged(state);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream);
            var buffer = new char[512];
            var lastUpdate = DateTimeOffset.UtcNow;
            while (!reader.EndOfStream)
            {
                var read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                state.AppendAiMarkdown(new string(buffer, 0, read));
                if (DateTimeOffset.UtcNow - lastUpdate > TimeSpan.FromMilliseconds(250))
                {
                    lastUpdate = DateTimeOffset.UtcNow;
                    NotifyStateChanged(state);
                }
            }

            if (state.EnableAiDebug)
            {
                state.SetAiStatus(AiStatus.Done);
            }
            NotifyStateChanged(state);
        }
        catch (HttpRequestException ex)
        {
            if (state.EnableAiDebug)
            {
                state.SetAiError(ex.Message);
                NotifyStateChanged(state);
            }
        }
    }

    private async Task<string?> GetOrDownloadImageAsync(string imageUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_imageCacheDirectory) ||
            !Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        if (_imageCache.TryGetValue(imageUrl, out var existing) && File.Exists(existing))
        {
            return existing;
        }

        try
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(imageUrl));
            var hash = Convert.ToHexString(hashBytes);
            var extension = ResolveImageExtension(uri.AbsolutePath);
            Directory.CreateDirectory(_imageCacheDirectory);
            var path = Path.Combine(_imageCacheDirectory, hash + extension);

            if (!File.Exists(path))
            {
                using var response = await httpClient
                    .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var destination = File.Create(path);
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            }

            var fileUri = new Uri(path).AbsoluteUri;
            _imageCache[imageUrl] = fileUri;
            return fileUri;
        }
        catch (HttpRequestException)
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

    private static string ResolveImageExtension(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension) || extension.Length > 5)
        {
            return ".img";
        }

        foreach (var c in extension)
        {
            if (!char.IsLetterOrDigit(c) && c != '.')
            {
                return ".img";
            }
        }

        return extension.ToLowerInvariant();
    }

    private static void ApplyAuthorizationHeader(HttpRequestMessage request, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey.Trim()}");
    }

    private static string? ExtractOpenAiChunk(string data)
    {
        using var document = JsonDocument.Parse(data);
        var root = document.RootElement;
        if (!TryGetProperty(root, "choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            return null;
        }

        var choice = choices[0];
        if (TryGetProperty(choice, "delta", out var delta))
        {
            var deltaContent = TryGetString(delta, "content");
            if (!string.IsNullOrEmpty(deltaContent))
            {
                return deltaContent;
            }
        }

        if (TryGetProperty(choice, "message", out var message))
        {
            var messageContent = TryGetString(message, "content");
            if (!string.IsNullOrEmpty(messageContent))
            {
                return messageContent;
            }
        }

        return TryGetString(choice, "text");
    }

    private static string BuildOpenAiRequestBody(string model, string prompt)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("model", string.IsNullOrWhiteSpace(model) ? "Meta-Llama-3_1-8B-Instruct" : model);
            writer.WriteBoolean("stream", true);
            writer.WriteNumber("max_tokens", 180);
            writer.WriteNumber("temperature", 0.2);
            writer.WriteStartArray("messages");
            writer.WriteStartObject();
            writer.WriteString("role", "user");
            writer.WriteString("content", prompt);
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static bool ShouldFetch(SearchSuggestion suggestion, SearchPreferences preferences, bool allowAiAnswer)
    {
        return preferences.ShowDetails &&
            (preferences.EnableRichWebDetails || allowAiAnswer && preferences.EnableAiAnswerDetails) &&
            suggestion.SourceKind is SuggestionSourceKind.SearchEngine or SuggestionSourceKind.SearchAnswer &&
            !string.IsNullOrWhiteSpace(suggestion.Query);
    }

    private static bool IsOpenAiCompatibleChatEndpoint(Uri endpoint, string endpointTemplate)
    {
        return endpoint.AbsolutePath.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase) ||
            endpointTemplate.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase);
    }

    private static Uri BuildEndpoint(string endpointTemplate, string prompt, string query, string language)
    {
        var encodedPrompt = Uri.EscapeDataString(prompt);
        var encodedPromptPlus = encodedPrompt.Replace("%20", "+", StringComparison.Ordinal);
        var encodedQuery = Uri.EscapeDataString(query);
        var encodedQueryPlus = encodedQuery.Replace("%20", "+", StringComparison.Ordinal);
        var raw = endpointTemplate
            .Replace("{prompt}", encodedPrompt, StringComparison.OrdinalIgnoreCase)
            .Replace("{prompt+}", encodedPromptPlus, StringComparison.OrdinalIgnoreCase)
            .Replace("{query}", encodedQuery, StringComparison.OrdinalIgnoreCase)
            .Replace("{query+}", encodedQueryPlus, StringComparison.OrdinalIgnoreCase)
            .Replace("{language}", Uri.EscapeDataString(language), StringComparison.OrdinalIgnoreCase)
            .Replace("%s", encodedQuery, StringComparison.OrdinalIgnoreCase);

        return Uri.TryCreate(raw, UriKind.Absolute, out var uri) ? uri : DefaultAiEndpoint;
    }

    private static string BuildKey(SearchSuggestion suggestion, SearchPreferences preferences, bool allowAiAnswer)
    {
        return string.Join(
            '|',
            suggestion.Query.ToLowerInvariant(),
            preferences.Language,
            preferences.EnableRichWebDetails,
            preferences.RichDetailsEndpointTemplate,
            allowAiAnswer && preferences.EnableAiAnswerDetails,
            preferences.AiAnswerEndpointTemplate,
            preferences.AiAnswerModel,
            HashApiKey(preferences.AiAnswerApiKey),
            preferences.EnableAiAnswerDebug);
    }

    private static string HashApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return string.Empty;
        }

        var bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(bytes, 0, 8);
    }

    private static string NormalizeWikipediaLanguage(string language)
    {
        var fallback = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
        if (string.IsNullOrWhiteSpace(fallback) || fallback.Equals("iv", StringComparison.OrdinalIgnoreCase))
        {
            fallback = "en";
        }

        var normalized = string.IsNullOrWhiteSpace(language) ? fallback : language.Split('-', StringSplitOptions.RemoveEmptyEntries)[0];
        return normalized.Length is >= 2 and <= 3 ? normalized.ToLowerInvariant() : fallback;
    }

    private static string ResolveAiPromptLanguage(string language)
    {
        var twoLetter = string.IsNullOrWhiteSpace(language)
            ? string.Empty
            : language.Split('-', StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();

        return twoLetter switch
        {
            "fr" => "français",
            "en" => "English",
            "es" => "español",
            "de" => "Deutsch",
            "it" => "italiano",
            "pt" => "português",
            "nl" => "Nederlands",
            "pl" => "polski",
            "ru" => "русский",
            "ja" => "日本語",
            "zh" => "中文",
            "ko" => "한국어",
            "ar" => "العربية",
            "tr" => "Türkçe",
            "sv" => "svenska",
            "no" => "norsk",
            "da" => "dansk",
            "fi" => "suomi",
            "cs" => "čeština",
            "hu" => "magyar",
            "el" => "ελληνικά",
            "he" => "עברית",
            "id" => "Bahasa Indonesia",
            "vi" => "Tiếng Việt",
            "th" => "ไทย",
            "ro" => "română",
            "uk" => "українська",
            _ => string.IsNullOrWhiteSpace(twoLetter) ? Strings.RichDetailsAiPromptLanguage : twoLetter,
        };
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var candidate in element.EnumerateObject())
            {
                if (candidate.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    property = candidate.Value;
                    return true;
                }
            }
        }

        property = default;
        return false;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return TryGetProperty(element, propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
    }

    private static string? TryGetAnyString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = TryGetString(element, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? TryGetNestedString(JsonElement element, params string[] propertyPath)
    {
        var current = element;
        foreach (var propertyName in propertyPath)
        {
            if (!TryGetProperty(current, propertyName, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static bool HasMeaningfulJsonValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => !string.IsNullOrWhiteSpace(value.GetString()),
            JsonValueKind.Array => value.GetArrayLength() > 0,
            JsonValueKind.Object => value.EnumerateObject().Any(),
            JsonValueKind.Null or JsonValueKind.Undefined => false,
            _ => true,
        };
    }

    private static void AppendPlainLine(StringBuilder markdown, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            markdown.AppendLine(EscapeMarkdown(Trim(text, 700)));
        }
    }

    private static string Trim(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : $"{value[..maxLength].TrimEnd()}...";
    }

    private static string EscapeMarkdown(string text)
    {
        return text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal);
    }

    private sealed record DetailOptions(bool EnableRichWebDetails, bool EnableAiAnswerDetails, bool EnableAiAnswerDebug);

    internal enum AiStatus
    {
        Idle,
        Disabled,
        Delaying,
        Requesting,
        Streaming,
        Done,
        Cancelled,
        Error,
    }

    private sealed class DetailState(DateTimeOffset createdAt, bool enableAiDebug)
    {
        private readonly object _lock = new();
        private readonly StringBuilder _aiMarkdown = new();
        private readonly List<Action<string?>> _observers = [];
        private bool _started;
        private bool _complete;
        private string? _webMarkdown;
        private AiStatus _aiStatus = AiStatus.Idle;
        private int _aiChunkCount;
        private string? _aiErrorMessage;
        private DateTimeOffset? _aiStartedAt;
        private DateTimeOffset? _aiCompletedAt;
        private string? _aiEndpoint;
        private string? _aiModel;

        public DateTimeOffset CreatedAt { get; } = createdAt;

        public bool EnableAiDebug { get; } = enableAiDebug;

        public bool TryStart()
        {
            lock (_lock)
            {
                if (_started)
                {
                    return false;
                }

                _started = true;
                return true;
            }
        }

        public void AddObserver(Action<string?> observer)
        {
            lock (_lock)
            {
                if (!_complete)
                {
                    _observers.Add(observer);
                }
            }
        }

        public void SetWebMarkdown(string markdown)
        {
            lock (_lock)
            {
                _webMarkdown = markdown;
            }
        }

        public void AppendAiMarkdown(string chunk)
        {
            lock (_lock)
            {
                _aiMarkdown.Append(chunk);
                _aiChunkCount++;
            }
        }

        public void SetAiStatus(AiStatus status)
        {
            lock (_lock)
            {
                _aiStatus = status;
                if (status == AiStatus.Requesting && _aiStartedAt is null)
                {
                    _aiStartedAt = DateTimeOffset.UtcNow;
                }

                if (status is AiStatus.Done or AiStatus.Cancelled or AiStatus.Error)
                {
                    _aiCompletedAt ??= DateTimeOffset.UtcNow;
                }
            }
        }

        public void SetAiError(string message)
        {
            lock (_lock)
            {
                _aiStatus = AiStatus.Error;
                _aiErrorMessage = message;
                _aiCompletedAt ??= DateTimeOffset.UtcNow;
            }
        }

        public void SetAiEndpoint(string endpoint, string model)
        {
            lock (_lock)
            {
                _aiEndpoint = endpoint;
                _aiModel = model;
            }
        }

        public void Complete()
        {
            lock (_lock)
            {
                _complete = true;
            }
        }

        public void NotifyChanged()
        {
            Action<string?>[] observers;
            string? markdown;
            lock (_lock)
            {
                observers = [.. _observers];
                markdown = ToMarkdownUnsafe();
            }

            foreach (var observer in observers)
            {
                observer(markdown);
            }
        }

        public void ClearObservers()
        {
            lock (_lock)
            {
                _observers.Clear();
            }
        }

        public string? ToMarkdown()
        {
            lock (_lock)
            {
                return ToMarkdownUnsafe();
            }
        }

        private string? ToMarkdownUnsafe()
        {
            var markdown = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(_webMarkdown))
            {
                markdown.AppendLine();
                markdown.AppendLine(_webMarkdown);
            }

            if (_aiMarkdown.Length > 0)
            {
                markdown.AppendLine();
                markdown.Append("#### ").AppendLine(Strings.RichDetailsHeaderAiAnswer);
                markdown.AppendLine(_aiMarkdown.ToString().Trim());
            }

            if (EnableAiDebug)
            {
                AppendAiDebugSection(markdown);
            }

            return markdown.Length == 0 ? null : markdown.ToString();
        }

        private void AppendAiDebugSection(StringBuilder markdown)
        {
            markdown.AppendLine();
            markdown.Append("#### ").AppendLine(Strings.RichDetailsAiDebugHeader);

            var statusLabel = _aiStatus switch
            {
                AiStatus.Idle => Strings.RichDetailsAiDebugIdle,
                AiStatus.Disabled => Strings.RichDetailsAiDebugDisabled,
                AiStatus.Delaying => Strings.RichDetailsAiDebugDelaying,
                AiStatus.Requesting => Strings.RichDetailsAiDebugRequesting,
                AiStatus.Streaming => Strings.RichDetailsAiDebugStreaming,
                AiStatus.Done => Strings.RichDetailsAiDebugDone,
                AiStatus.Cancelled => Strings.RichDetailsAiDebugCancelled,
                AiStatus.Error => Strings.RichDetailsAiDebugError(_aiErrorMessage ?? "?"),
                _ => _aiStatus.ToString(),
            };
            markdown.Append("- ");
            markdown.AppendLine(EscapeMarkdown(statusLabel));

            if (!string.IsNullOrWhiteSpace(_aiEndpoint))
            {
                markdown.Append("- ");
                markdown.AppendLine(EscapeMarkdown(Strings.RichDetailsAiDebugEndpoint(_aiEndpoint!)));
            }

            if (!string.IsNullOrWhiteSpace(_aiModel))
            {
                markdown.Append("- ");
                markdown.AppendLine(EscapeMarkdown(Strings.RichDetailsAiDebugModel(_aiModel!)));
            }

            markdown.Append("- ");
            markdown.AppendLine(EscapeMarkdown(Strings.RichDetailsAiDebugChunks(_aiChunkCount)));

            if (_aiStartedAt is { } start)
            {
                var end = _aiCompletedAt ?? DateTimeOffset.UtcNow;
                var duration = (int)Math.Max(0, (end - start).TotalMilliseconds);
                markdown.Append("- ");
                markdown.AppendLine(EscapeMarkdown(Strings.RichDetailsAiDebugDuration(duration)));
            }
        }
    }
}
