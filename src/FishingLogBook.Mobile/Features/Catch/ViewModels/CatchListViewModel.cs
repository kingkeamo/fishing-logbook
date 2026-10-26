using System.ComponentModel;
using System.Globalization;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Catch.Services;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Presentation;

namespace FishingLogBook.Mobile.Features.Catch.ViewModels;

public sealed class CatchListViewModel : INotifyPropertyChanged
{
    private ContentStateEnum _state;
    private string _searchTerm = string.Empty;
    private readonly IReadOnlyList<CatchPresentationModel> _samples;
    private CatchMethodEnum _method;

    public CatchListViewModel(UiTextService text, SampleCatchProvider samples)
    {
        Text = text;
        _samples = samples.Catches;
        Text.PropertyChanged += OnCultureChanged;
        Refresh();
    }

    public UiTextService Text { get; }
    public IReadOnlyList<CatchPresentationModel> Catches { get; private set; } = [];
    public IReadOnlyList<CatchGroupModel> Groups { get; private set; } = [];
    public string SearchTerm => _searchTerm;
    public CatchMethodEnum SelectedMethod => _method;
    public bool AllSelected => _method == CatchMethodEnum.All;
    public bool SpinningSelected => _method == CatchMethodEnum.Spinning;
    public bool FlySelected => _method == CatchMethodEnum.Fly;
    public bool HasFilters => !string.IsNullOrWhiteSpace(_searchTerm) || !AllSelected;
    public bool HasNoMatches => HasContent && Catches.Count == 0;
    public string ResultCount => string.Format(Text.Culture, Text["CatchCount"], Catches.Count);
    public ContentStateModel State { get; private set; } = null!;
    public bool HasContent => _state == ContentStateEnum.Ready;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetSearch(string? value)
    {
        _searchTerm = value ?? string.Empty;
        Refresh();
    }

    public void SetMethod(CatchMethodEnum value)
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
        _method = value;
        Refresh();
    }

    public void ClearFilters()
    {
        _searchTerm = string.Empty;
        _method = CatchMethodEnum.All;
        Refresh();
    }

    public void SetState(ContentStateEnum state)
    {
        if (!Enum.IsDefined(state) || state == ContentStateEnum.Planned)
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
        _state = state;
        Refresh();
    }

    private void OnCultureChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(UiTextService.Culture))
        {
            foreach (var sample in _samples)
            {
                sample.RefreshLocalizedText();
            }
            Refresh();
        }
    }

    private bool Matches(CatchPresentationModel item)
    {
        var matchesMethod = AllSelected || item.MethodIdentifier == _method;
        var query = _searchTerm.Trim();
        var fields = new[] { item.Species, item.Method, item.Place, item.Notes };
        return matchesMethod && (query.Length == 0 || fields.Any(field => Text.Culture.CompareInfo.IndexOf(field, query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0));
    }

    private void Refresh()
    {
        State = new ContentStateModel(_state, Text);
        Catches = !HasContent ? [] : _samples.Where(Matches).OrderByDescending(item => item.RecordedAt).ToArray();
        Groups = Catches.GroupBy(item => item.Day).Select(group => new CatchGroupModel(group.Key, group, Text)).ToArray();
        foreach (var property in new[] { nameof(Catches), nameof(Groups), nameof(SearchTerm), nameof(SelectedMethod), nameof(AllSelected), nameof(SpinningSelected), nameof(FlySelected), nameof(HasFilters), nameof(HasNoMatches), nameof(ResultCount) })
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(State)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasContent)));
    }
}
