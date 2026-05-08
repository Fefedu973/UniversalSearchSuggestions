using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using UniversalSearchSuggestions.Core.Search;
using Windows.Foundation;

namespace UniversalSearchSuggestions.RichDetails;

internal sealed partial class LazySearchDetails : IDetails, INotifyPropChanged
{
    private readonly object _lock = new();
    private readonly RichDetailsService _service;
    private readonly SearchSuggestion _suggestion;
    private readonly SearchPreferences _preferences;
    private readonly bool _enableExternalDetails;
    private readonly bool _allowAiAnswer;
    private string _baseMarkdown;
    private string? _richMarkdown;
    private bool _started;

    public LazySearchDetails(
        RichDetailsService service,
        SearchSuggestion suggestion,
        SearchPreferences preferences,
        string baseMarkdown,
        bool enableExternalDetails,
        bool allowAiAnswer)
    {
        _service = service;
        _suggestion = suggestion;
        _preferences = preferences;
        _baseMarkdown = baseMarkdown;
        _enableExternalDetails = enableExternalDetails;
        _allowAiAnswer = allowAiAnswer;
    }

    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged;

    public string Title { get; set; } = string.Empty;

    public string Body
    {
        get
        {
            EnsureStarted();
            lock (_lock)
            {
                return BuildBodyUnsafe();
            }
        }
        set
        {
            lock (_lock)
            {
                _baseMarkdown = value;
            }

            RaisePropertyChanged(nameof(Body));
        }
    }

    public IIconInfo HeroImage { get; set; } = null!;

    public IDetailsElement[] Metadata { get; set; } = [];

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

        if (!_enableExternalDetails)
        {
            return;
        }

        var markdown = _service.GetCachedMarkdownOrQueue(
            _suggestion,
            _preferences,
            _allowAiAnswer,
            OnRichMarkdownChanged);
        if (!string.IsNullOrWhiteSpace(markdown))
        {
            OnRichMarkdownChanged(markdown);
        }
    }

    private void OnRichMarkdownChanged(string? markdown)
    {
        lock (_lock)
        {
            _richMarkdown = markdown;
        }

        RaisePropertyChanged(nameof(Body));
    }

    private string BuildBodyUnsafe()
    {
        if (string.IsNullOrWhiteSpace(_richMarkdown))
        {
            return _baseMarkdown;
        }

        return string.Concat(_baseMarkdown, Environment.NewLine, _richMarkdown);
    }

    private void RaisePropertyChanged(string propertyName)
    {
        PropChanged?.Invoke(this, new PropChangedEventArgs(propertyName));
    }
}
