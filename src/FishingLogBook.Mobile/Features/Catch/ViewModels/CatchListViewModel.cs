using System.ComponentModel;
using System.Globalization;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Catch.Services;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Presentation;
using FishingLogBook.Shared.Measurements;

namespace FishingLogBook.Mobile.Features.Catch.ViewModels;

public sealed class CatchListViewModel : INotifyPropertyChanged
{
    private static readonly CatchSpeciesEnum[] SpeciesOrder =
    [
        CatchSpeciesEnum.AtlanticSalmon,
        CatchSpeciesEnum.BrownTrout,
        CatchSpeciesEnum.RainbowTrout,
        CatchSpeciesEnum.Pike,
        CatchSpeciesEnum.SeaTrout,
        CatchSpeciesEnum.Other
    ];

    private readonly IReadOnlyList<CatchPresentationModel> _samples;
    private readonly IAccountUnitsService _units;
    private readonly IMeasurementService _measurement;
    private readonly TimeProvider _clock;
    private ContentStateEnum _state;
    private string _searchTerm = string.Empty;
    private CatchFilterModel _filters = CatchFilterModel.None;

    public CatchListViewModel(
        UiTextService text,
        SampleCatchProvider samples,
        IAccountUnitsService units,
        IMeasurementService measurement,
        TimeProvider clock)
    {
        Text = text;
        _samples = samples.Catches;
        _units = units;
        _measurement = measurement;
        _clock = clock;
        Text.PropertyChanged += OnCultureChanged;
        Refresh();
    }

    public UiTextService Text { get; }
    public IReadOnlyList<CatchPresentationModel> Catches { get; private set; } = [];
    public IReadOnlyList<CatchGroupModel> Groups { get; private set; } = [];
    public string SearchTerm => _searchTerm;
    public CatchFilterModel Filters => _filters;
    public CatchMethodEnum SelectedMethod => _filters.Method;
    public bool AllSelected => SelectedMethod == CatchMethodEnum.All;
    public bool SpinningSelected => SelectedMethod == CatchMethodEnum.Spinning;
    public bool FlySelected => SelectedMethod == CatchMethodEnum.Fly;
    public bool LureSelected => SelectedMethod == CatchMethodEnum.Lure;
    public bool HasFilters => !string.IsNullOrWhiteSpace(_searchTerm) || _filters.ActiveCount > 0;
    public bool HasAdvancedFilters => _filters.ActiveCount > 0;
    public int ActiveFilterCount => _filters.ActiveCount;
    public bool HasNoMatches => HasContent && Catches.Count == 0;
    public bool IsEmptyJournal => _state == ContentStateEnum.Empty;
    public string ResultCount => FormatCount(Catches.Count);
    public ContentStateModel State { get; private set; } = null!;
    public bool HasContent => _state == ContentStateEnum.Ready;

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task LoadUnitsAsync(CancellationToken cancellationToken)
    {
        var units = await _units.GetAsync(cancellationToken);
        foreach (var item in _samples)
        {
            item.SetUnits(_measurement, units.WeightUnit, units.LengthUnit);
        }

        Refresh();
    }

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

        _filters = _filters with { Method = value };
        Refresh();
    }

    public void ApplyFilters(CatchFilterModel filters)
    {
        _filters = filters;
        Refresh();
    }

    public CatchFilterDraftModel CreateFilterDraft()
    {
        var options = SpeciesOrder
            .Select(species => new SpeciesOptionModel(species, Text["Species" + species], _samples.Count(item => item.SpeciesIdentifier == species), _filters.Species.Contains(species)))
            .ToArray();
        return new CatchFilterDraftModel(Text, _filters, options);
    }

    public string FormatCount(int count)
    {
        return string.Format(Text.Culture, Text[count == 1 ? "CatchCountSingle" : "CatchCount"], count);
    }

    public void ClearFilters()
    {
        _searchTerm = string.Empty;
        _filters = CatchFilterModel.None;
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
        if (args.PropertyName != nameof(UiTextService.Culture))
        {
            return;
        }

        foreach (var sample in _samples)
        {
            sample.RefreshLocalizedText();
        }

        Refresh();
    }

    private bool Matches(CatchPresentationModel item)
    {
        return MatchesFilters(item) && MatchesSearch(item);
    }

    private bool MatchesFilters(CatchPresentationModel item)
    {
        var matchesMethod = AllSelected || item.MethodIdentifier == _filters.Method;
        var matchesSpecies = _filters.Species.Count == 0 || _filters.Species.Contains(item.SpeciesIdentifier);
        return matchesMethod && matchesSpecies && MatchesDateRange(item) && Contains(item.Place, _filters.Location);
    }

    private bool MatchesDateRange(CatchPresentationModel item)
    {
        var days = _filters.DateRange switch
        {
            CatchDateRangeEnum.Last7Days => 7,
            CatchDateRangeEnum.Last30Days => 30,
            _ => 0
        };
        return days == 0 || item.RecordedAt > _clock.GetUtcNow().AddDays(-days);
    }

    private bool MatchesSearch(CatchPresentationModel item)
    {
        var fields = new[] { item.Species, item.Method, item.Place, item.Notes };
        return fields.Any(field => Contains(field, _searchTerm));
    }

    private bool Contains(string field, string query)
    {
        var trimmed = query.Trim();
        return trimmed.Length == 0 || Text.Culture.CompareInfo.IndexOf(field, trimmed, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
    }

    private void Refresh()
    {
        State = new ContentStateModel(_state, Text);
        Catches = !HasContent ? [] : _samples.Where(Matches).OrderByDescending(item => item.RecordedAt).ToArray();
        Groups = Catches.GroupBy(item => item.Day).Select(group => new CatchGroupModel(group.Key, group, Text)).ToArray();
        foreach (var property in new[]
                 {
                     nameof(Catches), nameof(Groups), nameof(SearchTerm), nameof(Filters), nameof(SelectedMethod), nameof(AllSelected),
                     nameof(SpinningSelected), nameof(FlySelected), nameof(LureSelected), nameof(HasFilters), nameof(HasAdvancedFilters),
                     nameof(ActiveFilterCount), nameof(HasNoMatches), nameof(IsEmptyJournal), nameof(ResultCount), nameof(State), nameof(HasContent)
                 })
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }
}
