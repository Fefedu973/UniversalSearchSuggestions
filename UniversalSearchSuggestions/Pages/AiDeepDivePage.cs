using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using UniversalSearchSuggestions.Commands;
using UniversalSearchSuggestions.Core.Browsers;
using UniversalSearchSuggestions.Core.Resources;
using UniversalSearchSuggestions.Core.Search;
using UniversalSearchSuggestions.Icons;
using UniversalSearchSuggestions.RichDetails;

namespace UniversalSearchSuggestions.Pages;

internal sealed partial class AiDeepDivePage : ContentPage, IDisposable
{
    internal const string FormPromptId = "prompt";
    internal const string FormProviderId = "provider";
    internal const string FormActionId = "action";
    internal const string FormActionSend = "send";
    internal const string FormActionOpen = "open";
    private const int MaxWebPromptLength = 6000;

    private static readonly WebChatProvider[] DefaultWebChatProviders =
    [
        new("chatgpt", "ChatGPT", "chatgpt.com", "https://chatgpt.com/?hints=search&q={prompt}", Enabled: true),
        new("claude", "Claude", "claude.ai", "https://claude.ai/new?q={prompt}", Enabled: true),
        new("gemini", "Gemini", "gemini.google.com", "https://gemini.google.com/app?q={prompt}", Enabled: true),
        new("copilot", "Copilot", "copilot.microsoft.com", "https://copilot.microsoft.com/?q={prompt}", Enabled: true),
        new("perplexity", "Perplexity", "perplexity.ai", "https://www.perplexity.ai/?q={prompt}", Enabled: true),
        new("mistral", "Le Chat", "chat.mistral.ai", "https://chat.mistral.ai/chat?q={prompt}", Enabled: true),
        new("grok", "Grok", "grok.com", "https://grok.com/?q={prompt}", Enabled: true),
        new("t3chat", "T3 Chat", "t3.chat", "https://t3.chat/new?q={prompt}", Enabled: true),
    ];

    private readonly RichDetailsService _service;
    private readonly string _query;
    private readonly SearchPreferences _preferences;
    private readonly StreamingMarkdownContent _content;
    private readonly DeepDiveFormContent _form;
    private readonly List<ChatTurn> _conversation = [];
    private readonly StringBuilder _currentAnswer = new();
    private readonly object _lock = new();
    private readonly object _streamLock = new();
    private readonly object _conversationLock = new();
    private CancellationTokenSource? _cts;
    private bool _started;
    private int _streamVersion;

    public AiDeepDivePage(RichDetailsService service, string query, SearchPreferences preferences)
    {
        _service = service;
        _query = query;
        _preferences = preferences;
        _content = new StreamingMarkdownContent(BuildLoadingHeader(query));
        _form = new DeepDiveFormContent(DefaultWebChatProviders, HandleFormSubmit);

        lock (_conversationLock)
        {
            _conversation.Add(new ChatTurn(ChatRole.User, _query));
        }

        Id = "com.fefedu973.universalsearchsuggestions.deepdive." + Guid.NewGuid().ToString("N");
        Icon = AppIcons.Ai;
        Title = $"{Strings.DeepDiveTitle}: {query}";
        Name = Strings.DeepDiveTitle;
    }

    public override IContent[] GetContent()
    {
        EnsureStarted();
        return [_content, _form];
    }

    private void EnsureStarted()
    {
        lock (_lock)
        {
            if (_started)
            {
                return;
            }

            _started = true;
        }

        if (!CanStreamAi)
        {
            _content.SetBody(BuildHeader(_query) + Environment.NewLine + Strings.DeepDiveDisabled);
            return;
        }

        StartStream(_query);
    }

    private bool CanStreamAi => _preferences.EnableAiAnswerDetails &&
        !string.IsNullOrWhiteSpace(_preferences.AiAnswerEndpointTemplate);

    private CommandResult HandleFormSubmit(DeepDiveFormPayload payload)
    {
        var action = payload.Action?.Trim();
        if (string.Equals(action, FormActionSend, StringComparison.OrdinalIgnoreCase))
        {
            return HandleSend(payload.Prompt);
        }

        if (string.Equals(action, FormActionOpen, StringComparison.OrdinalIgnoreCase))
        {
            return HandleOpenInWeb(payload.ProviderId, payload.Prompt);
        }

        return CommandResult.KeepOpen();
    }

    private CommandResult HandleSend(string? prompt)
    {
        var trimmed = prompt?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return CommandResult.KeepOpen();
        }

        if (!CanStreamAi)
        {
            _content.AppendBody($"\n\n> {Strings.DeepDiveDisabled}\n");
            return CommandResult.KeepOpen();
        }

        lock (_conversationLock)
        {
            _conversation.Add(new ChatTurn(ChatRole.User, trimmed));
        }
        AppendUserBlock(trimmed);
        StartAssistantSection();
        StartStream(BuildConversationPrompt());
        return CommandResult.KeepOpen();
    }

    private CommandResult HandleOpenInWeb(string? providerId, string? prompt)
    {
        var provider = DefaultWebChatProviders
            .FirstOrDefault(entry => entry.Enabled && string.Equals(entry.Id, providerId, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            return CommandResult.KeepOpen();
        }

        var resolvedPrompt = BuildWebChatPrompt(prompt);
        var target = provider.BuildUrl(resolvedPrompt);
        var browser = BrowserInstallDetector.Resolve(_preferences.BrowserId, _preferences.CustomBrowserPath);
        BrowserLauncher.Open(target, browser, profile: null, privateMode: false);
        return CommandResult.Hide();
    }

    private void StartStream(string prompt)
    {
        var version = Interlocked.Increment(ref _streamVersion);
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        lock (_streamLock)
        {
            _currentAnswer.Clear();
        }

        _ = StreamAsync(prompt, version, _cts.Token);
    }

    private async Task StreamAsync(string prompt, int version, CancellationToken cancellationToken)
    {
        try
        {
            await _service.StreamAiAnswerAsync(
                prompt,
                _preferences,
                chunk => AppendAssistantChunk(chunk, version),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested && version == Volatile.Read(ref _streamVersion))
            {
                FinishAssistantTurn();
            }
        }
    }

    private static string BuildHeader(string query) => $"# {Strings.DeepDiveTitle}\n\n> {EscapeMarkdown(query)}\n\n";

    private static string BuildLoadingHeader(string query)
    {
        return BuildHeader(query) +
            $"**{Strings.DeepDiveAssistantLabel}**\n\n" +
            Strings.DeepDiveLoading;
    }

    private void AppendAssistantChunk(string chunk, int version)
    {
        if (version != Volatile.Read(ref _streamVersion))
        {
            return;
        }

        lock (_streamLock)
        {
            _currentAnswer.Append(chunk);
        }

        _content.AppendBody(chunk);
    }

    private void FinishAssistantTurn()
    {
        string answer;
        lock (_streamLock)
        {
            answer = _currentAnswer.ToString().Trim();
        }

        if (string.IsNullOrWhiteSpace(answer))
        {
            return;
        }

        lock (_conversationLock)
        {
            _conversation.Add(new ChatTurn(ChatRole.Assistant, answer));
        }
    }

    private void AppendUserBlock(string prompt)
    {
        var escaped = EscapeMarkdown(prompt);
        _content.AppendBody($"\n\n---\n\n**{Strings.DeepDiveYouLabel}**\n\n{escaped}\n\n");
    }

    private void StartAssistantSection()
    {
        _content.AppendBody($"**{Strings.DeepDiveAssistantLabel}**\n\n");
    }

    private string BuildWebChatPrompt(string? prompt)
    {
        ChatTurn[] turns;
        lock (_conversationLock)
        {
            turns = [.. _conversation];
        }

        var builder = new StringBuilder();
        var trimmed = prompt?.Trim();

        if (turns.Length > 0)
        {
            builder.AppendLine("Conversation so far:");
            foreach (var turn in turns)
            {
                var role = turn.Role == ChatRole.User ? "User" : "Assistant";
                builder.Append(role);
                builder.Append(": ");
                builder.AppendLine(turn.Text);
                builder.AppendLine();
            }
        }

        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            builder.Append("User: ");
            builder.AppendLine(trimmed);
        }

        var result = builder.Length == 0 ? _query : builder.ToString().Trim();
        if (result.Length <= MaxWebPromptLength)
        {
            return result;
        }

        return result[^MaxWebPromptLength..];
    }

    private string BuildConversationPrompt()
    {
        ChatTurn[] turns;
        lock (_conversationLock)
        {
            turns = [.. _conversation];
        }

        var builder = new StringBuilder();
        builder.AppendLine("Conversation so far:");
        foreach (var turn in turns)
        {
            var role = turn.Role == ChatRole.User ? "User" : "Assistant";
            builder.Append(role);
            builder.Append(": ");
            builder.AppendLine(turn.Text);
            builder.AppendLine();
        }

        builder.Append("Assistant:");
        return builder.ToString();
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

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}

internal sealed partial class DeepDiveFormContent : FormContent
{
    private readonly Func<DeepDiveFormPayload, CommandResult> _onSubmit;

    public DeepDiveFormContent(
        IReadOnlyList<WebChatProvider> providers,
        Func<DeepDiveFormPayload, CommandResult> onSubmit)
    {
        _onSubmit = onSubmit;
        TemplateJson = BuildTemplateJson(providers);
    }

    public override CommandResult SubmitForm(string payload)
    {
        return SubmitFormCore(payload, null);
    }

    public override CommandResult SubmitForm(string payload, string state)
    {
        return SubmitFormCore(payload, state);
    }

    private CommandResult SubmitFormCore(string payload, string? state)
    {
        try
        {
            var input = JsonNode.Parse(payload)?.AsObject() ?? new JsonObject();

            var action = input[AiDeepDivePage.FormActionId]?.ToString();
            var providerId = input[AiDeepDivePage.FormProviderId]?.ToString();
            var prompt = input[AiDeepDivePage.FormPromptId]?.ToString();
            if (string.IsNullOrWhiteSpace(action))
            {
                action = TryReadActionFromState(state);
            }

            if (string.IsNullOrWhiteSpace(action))
            {
                return CommandResult.KeepOpen();
            }

            return _onSubmit(new DeepDiveFormPayload(action, providerId, prompt));
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return CommandResult.KeepOpen();
        }
    }

    private static string? TryReadActionFromState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        try
        {
            var node = JsonNode.Parse(state);
            if (node is JsonObject obj)
            {
                return obj[AiDeepDivePage.FormActionId]?.ToString();
            }

            return node?.ToString();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string BuildTemplateJson(IReadOnlyList<WebChatProvider> providers)
    {
        var enabledProviders = providers.Where(provider => provider.Enabled).ToArray();
        var body = new JsonArray();

        AddNode(body, new JsonObject
        {
            ["type"] = "TextBlock",
            ["size"] = "medium",
            ["weight"] = "bolder",
            ["text"] = Strings.DeepDiveContinueLabel,
            ["wrap"] = true,
        });

        AddNode(body, new JsonObject
        {
            ["type"] = "Input.Text",
            ["id"] = AiDeepDivePage.FormPromptId,
            ["style"] = "text",
            ["isMultiline"] = true,
            ["placeholder"] = Strings.DeepDiveContinuePlaceholder,
        });

        if (enabledProviders.Length > 0)
        {
            AddNode(body, new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = Strings.DeepDiveOpenInWebLabel,
                ["wrap"] = true,
                ["spacing"] = "Medium",
            });

            var choices = new JsonArray();
            foreach (var provider in enabledProviders)
            {
                AddNode(choices, new JsonObject
                {
                    ["title"] = provider.Name,
                    ["value"] = provider.Id,
                });
            }

            AddNode(body, new JsonObject
            {
                ["type"] = "Input.ChoiceSet",
                ["id"] = AiDeepDivePage.FormProviderId,
                ["style"] = "compact",
                ["value"] = enabledProviders[0].Id,
                ["choices"] = choices,
            });
        }

        var actions = new JsonArray();

        AddNode(actions, new JsonObject
        {
            ["type"] = "Action.Submit",
            ["title"] = Strings.DeepDiveSend,
            ["associatedInputs"] = "auto",
            ["data"] = new JsonObject
            {
                [AiDeepDivePage.FormActionId] = AiDeepDivePage.FormActionSend,
            },
        });

        if (enabledProviders.Length > 0)
        {
            AddNode(actions, new JsonObject
            {
                ["type"] = "Action.Submit",
                ["title"] = Strings.DeepDiveOpenInWebAction,
                ["associatedInputs"] = "auto",
                ["data"] = new JsonObject
                {
                    [AiDeepDivePage.FormActionId] = AiDeepDivePage.FormActionOpen,
                },
            });
        }

        var card = new JsonObject
        {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["type"] = "AdaptiveCard",
            ["version"] = "1.6",
            ["body"] = body,
            ["actions"] = actions,
        };

        return card.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static void AddNode(JsonArray array, JsonNode node)
    {
        array.Add(node);
    }
}

internal sealed record DeepDiveFormPayload(string? Action, string? ProviderId, string? Prompt);

internal sealed record WebChatProvider(string Id, string Name, string Domain, string UrlTemplate, bool Enabled)
{
    public Uri BuildUrl(string prompt)
    {
        var encoded = Uri.EscapeDataString(prompt ?? string.Empty);
        var resolved = UrlTemplate.Replace("{prompt}", encoded, StringComparison.Ordinal);
        return new Uri(resolved, UriKind.Absolute);
    }
}

internal sealed record ChatTurn(ChatRole Role, string Text);

internal enum ChatRole
{
    User,
    Assistant,
}

internal sealed partial class StreamingMarkdownContent : BaseObservable, IMarkdownContent
{
    private readonly StringBuilder _body;
    private string _initialPlaceholder;
    private bool _placeholderVisible = true;

    public StreamingMarkdownContent(string initialBody)
    {
        _body = new StringBuilder();
        _initialPlaceholder = initialBody ?? string.Empty;
    }

    public string Body
    {
        get
        {
            lock (_body)
            {
                if (_placeholderVisible || _body.Length == 0)
                {
                    return _initialPlaceholder;
                }

                return _initialPlaceholder.Substring(0, FindPlaceholderHeader().Length) + _body;
            }
        }
    }

    public void AppendBody(string chunk)
    {
        lock (_body)
        {
            _placeholderVisible = false;
            _body.Append(chunk);
        }

        OnPropertyChanged(nameof(Body));
    }

    public void SetBody(string body)
    {
        lock (_body)
        {
            _initialPlaceholder = body;
            _body.Clear();
            _placeholderVisible = true;
        }

        OnPropertyChanged(nameof(Body));
    }

    private string FindPlaceholderHeader()
    {
        // Strip the trailing "Asking the model…" suffix from the placeholder so streaming chunks
        // append onto the title/quote header without re-appending the loading marker.
        var loading = Strings.DeepDiveLoading;
        var idx = _initialPlaceholder.LastIndexOf(loading, StringComparison.Ordinal);
        return idx < 0 ? _initialPlaceholder : _initialPlaceholder[..idx];
    }
}
