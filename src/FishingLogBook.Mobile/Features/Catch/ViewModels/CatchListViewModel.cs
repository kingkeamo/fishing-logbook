using System.ComponentModel;
using System.Globalization;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Presentation;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Catch.ViewModels;

public sealed class CatchListViewModel : INotifyPropertyChanged
{
    private ContentStateEnum _state;
    private string _searchTerm = string.Empty;
    private string _method = string.Empty;

    public CatchListViewModel(UiTextService text)
    {
        Text = text;
        Text.PropertyChanged += OnCultureChanged;
        Refresh();
    }

    public UiTextService Text { get; }
    public IReadOnlyList<CatchPresentationModel> Catches { get; private set; } = [];
    public IReadOnlyList<CatchGroupModel> Groups { get; private set; } = [];
    public string SearchTerm => _searchTerm;
    public string SelectedMethod => _method;
    public bool AllSelected => _method.Length == 0;
    public bool SpinningSelected => _method == "Spinning";
    public bool FlySelected => _method == "Fly";
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

    public void SetMethod(string value)
    {
        if (value is not ("" or "Spinning" or "Fly"))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
        _method = value;
        Refresh();
    }

    public void ClearFilters()
    {
        _searchTerm = string.Empty;
        _method = string.Empty;
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
            Refresh();
        }
    }

    private bool Matches(CatchPresentationModel item)
    {
        var matchesMethod = AllSelected || item.MethodCode == _method;
        var query = _searchTerm.Trim();
        var fields = new[] { item.Species, item.Method, item.Place, item.Notes };
        return matchesMethod && (query.Length == 0 || fields.Any(field => Text.Culture.CompareInfo.IndexOf(field, query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0));
    }

    private void Refresh()
    {
        var caughtOn = new DateTimeOffset(2026, 6, 14, 7, 30, 0, TimeSpan.Zero);
        State = new ContentStateModel(_state, Text);
        CatchPresentationModel[] samples =
        [
            new CatchPresentationModel(new CatchDto(Guid.Parse("00000000-0000-0000-0000-000000000001"), caughtOn, [])
            {
                SpeciesName = Text["SampleSpecies"],
                Method = "Fly",
                Weight = 2.4m,
                Length = 48m,
                PlaceName = Text["SamplePlace"],
                Notes = Text["SampleNotes"]
            }, Text, "sample_trout.jpg", isSample: true),
            new CatchPresentationModel(new CatchDto(Guid.Parse("00000000-0000-0000-0000-000000000002"), caughtOn.AddDays(-1), []), Text),
            new CatchPresentationModel(new CatchDto(Guid.Parse("00000000-0000-0000-0000-000000000003"), caughtOn.AddHours(-1), [])
            {
                SpeciesName = Text["SampleSalmon"], Method = "Spinning", Weight = 3.6m, Length = 68m
            }, Text, isSample: true),
            new CatchPresentationModel(new CatchDto(Guid.Parse("00000000-0000-0000-0000-000000000004"), caughtOn.AddDays(-1).AddHours(2), [])
            {
                SpeciesName = Text["SampleSpecies"], Method = "Spinning", Weight = 0.8m, Length = 29m
            }, Text, "sample_trout.jpg", isSample: true)
        ];
        Catches = !HasContent ? [] : samples.Where(Matches).OrderByDescending(item => item.RecordedAt).ToArray();
        Groups = Catches.GroupBy(item => item.Day).Select(group => new CatchGroupModel(group.Key, group, Text)).ToArray();
        foreach (var property in new[] { nameof(Catches), nameof(Groups), nameof(SearchTerm), nameof(SelectedMethod), nameof(AllSelected), nameof(SpinningSelected), nameof(FlySelected), nameof(HasFilters), nameof(HasNoMatches), nameof(ResultCount) })
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(State)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasContent)));
    }
}
