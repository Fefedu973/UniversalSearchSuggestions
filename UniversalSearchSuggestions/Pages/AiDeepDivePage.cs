using System.Text;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using UniversalSearchSuggestions.Core.Resources;
using UniversalSearchSuggestions.Core.Search;
using UniversalSearchSuggestions.Icons;
using UniversalSearchSuggestions.RichDetails;

namespace UniversalSearchSuggestions.Pages;

internal sealed partial class AiDeepDivePage : ContentPage, IDisposable
{
    private readonly RichDetailsService _service;
    private readonly string _query;
    private readonly SearchPreferences _preferences;
    private readonly StreamingMarkdownContent _content;
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private bool _started;

    public AiDeepDivePage(RichDetailsService service, string query, SearchPreferences preferences)
    {
        _service = service;
        _query = query;
        _preferences = preferences;
        _content = new StreamingMarkdownContent(BuildLoadingHeader(query));

        Id = "com.fefedu973.universalsearchsuggestions.deepdive." + Guid.NewGuid().ToString("N");
        Icon = AppIcons.Ai;
        Title = $"{Strings.DeepDiveTitle}: {query}";
        Name = Strings.DeepDiveTitle;
    }

    public override IContent[] GetContent()
    {
        EnsureStarted();
        return [_content];
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

        if (!_preferences.EnableAiAnswerDetails || string.IsNullOrWhiteSpace(_preferences.AiAnswerEndpointTemplate))
        {
            _content.SetBody(BuildHeader(_query) + Environment.NewLine + Strings.DeepDiveDisabled);
            return;
        }

        _cts = new CancellationTokenSource();
        _ = StreamAsync(_cts.Token);
    }

    private async Task StreamAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _service.StreamAiAnswerAsync(
                _query,
                _preferences,
                _content.AppendBody,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static string BuildHeader(string query) => $"# {Strings.DeepDiveTitle}\n\n> {EscapeMarkdown(query)}\n\n";

    private static string BuildLoadingHeader(string query) => BuildHeader(query) + Strings.DeepDiveLoading;

    private static string EscapeMarkdown(string text)
    {
        return text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
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
