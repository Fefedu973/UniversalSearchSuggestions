using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using UniversalSearchSuggestions.Core.Resources;
using UniversalSearchSuggestions.Core.Search;
using UniversalSearchSuggestions.Icons;
using UniversalSearchSuggestions.Pages;
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
        Metadata = BuildMetadata(suggestion, preferences, allowAiAnswer);
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

    public IDetailsElement[] Metadata { get; set; }

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

        if (!_enableExternalDetails && !_allowAiAnswer)
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

    private static IDetailsElement[] BuildMetadata(
        SearchSuggestion suggestion,
        SearchPreferences preferences,
        bool allowAiAnswer)
    {
        var elements = new List<IDetailsElement>();
        var tags = SuggestionTagBuilder.BuildTags(suggestion);

        if (suggestion.IsCurrentQueryAction)
        {
            tags = AppendTag(tags, BuildSourceTag(preferences));
            if (allowAiAnswer && preferences.EnableAiAnswerDetails)
            {
                tags = AppendTag(tags, BuildAiTag());
            }
        }

        if (tags.Length > 0)
        {
            elements.Add(new DetailsElement
            {
                Key = Strings.DetailSource,
                Data = new DetailsTags { Tags = tags },
            });
        }

        if (!suggestion.IsNavigation &&
            suggestion.SourceKind is SuggestionSourceKind.SearchEngine or SuggestionSourceKind.SearchAnswer)
        {
            elements.Add(new DetailsElement
            {
                Key = Strings.DetailAction,
                Data = new DetailsLink
                {
                    Text = Strings.DetailOpenInBrowser,
                    Link = suggestion.TargetUri,
                },
            });
        }

        return [.. elements];
    }

    private static Tag BuildSourceTag(SearchPreferences preferences)
    {
        var engine = SearchEngineCatalog.Get(preferences.PrimaryEngine);
        return new Tag(engine.DisplayName)
        {
            Icon = AppIcons.Globe,
            ToolTip = Strings.DetailSearchWith(engine.DisplayName),
        };
    }

    private static Tag BuildAiTag()
    {
        return new Tag(Strings.TagAi)
        {
            Icon = AppIcons.Ai,
            ToolTip = Strings.RichDetailsHeaderAiAnswer,
        };
    }

    private static ITag[] AppendTag(ITag[] tags, ITag extra)
    {
        if (tags.Length == 0)
        {
            return [extra];
        }

        var result = new ITag[tags.Length + 1];
        Array.Copy(tags, result, tags.Length);
        result[^1] = extra;
        return result;
    }
}
