using System.ComponentModel;
using FishingLogBook.Mobile.Features.Dashboard.Models;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Dashboard.Models;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Mobile.Features.Dashboard.ViewModels;

public sealed class DashboardFilterViewModel(UiTextService text, TimeProvider clock) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public UiTextService Text { get; } = text;
    public IReadOnlyList<DashboardChoiceModel> SpeciesOptions { get; private set; } = [];
    public IReadOnlyList<DashboardChoiceModel> MethodOptions { get; private set; } = [];
    public IReadOnlyList<DashboardChoiceModel> VenueOptions { get; private set; } = [];
    public DashboardChoiceModel? SelectedSpecies { get; set; }
    public DashboardChoiceModel? SelectedMethod { get; set; }
    public DashboardChoiceModel? SelectedVenue { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int PresetIndex { get; private set; } = (int)DashboardDatePresetEnum.ThisYear;
    public bool IsCustom => PresetIndex == (int)DashboardDatePresetEnum.Custom;
    public bool HasValidationError { get; private set; }
    public string ValidationText => Text["DashboardInvalidDates"];
    public IReadOnlyList<string> DatePresets => Enum.GetValues<DashboardDatePresetEnum>()
        .Select(preset => Text[$"Dashboard{preset}"]).ToArray();

    public void Initialize(DashboardFilterSelectionModel selection, DashboardFilterOptionsModel options)
    {
        var today = clock.GetLocalNow();
        PresetIndex = (int)selection.Preset;
        FromDate = selection.From == default ? new DateTime(today.Year, 1, 1) : selection.From;
        ToDate = selection.To == default ? new DateTime(today.Year, 12, 31) : selection.To;
        SpeciesOptions = Choices(options.Species, selection.Species);
        MethodOptions = Choices(options.Methods, selection.Method);
        VenueOptions = Choices(options.Venues, selection.Venue);
        SelectedSpecies = SpeciesOptions.FirstOrDefault(choice => choice.Value == selection.Species);
        SelectedMethod = MethodOptions.FirstOrDefault(choice => choice.Value == selection.Method);
        SelectedVenue = VenueOptions.FirstOrDefault(choice => choice.Value == selection.Venue);
        HasValidationError = false;
        Notify();
    }

    public void SelectPreset(int index)
    {
        if (index == PresetIndex || !Enum.IsDefined((DashboardDatePresetEnum)index))
        {
            return;
        }

        PresetIndex = index;
        HasValidationError = false;
        Notify();
    }

    public DashboardFilterSelectionModel? Apply()
    {
        var range = DashboardDateRangeModel.Resolve((DashboardDatePresetEnum)PresetIndex,
            DateOnly.FromDateTime(clock.GetLocalNow().DateTime), DateOnly.FromDateTime(FromDate), DateOnly.FromDateTime(ToDate));
        HasValidationError = range.From > range.To || range.To == DateOnly.MaxValue;
        Notify();
        if (HasValidationError)
        {
            return null;
        }

        return new DashboardFilterSelectionModel
        {
            Preset = (DashboardDatePresetEnum)PresetIndex,
            From = FromDate,
            To = ToDate,
            Species = SelectedSpecies?.Value,
            Method = SelectedMethod?.Value,
            Venue = SelectedVenue?.Value
        };
    }

    public DashboardFilterSelectionModel Reset()
    {
        var today = clock.GetLocalNow();
        return new DashboardFilterSelectionModel
        {
            From = new DateTime(today.Year, 1, 1),
            To = new DateTime(today.Year, 12, 31)
        };
    }

    private IReadOnlyList<DashboardChoiceModel> Choices(IReadOnlyList<string> values, string? selected)
    {
        var choices = values.Select(value => new DashboardChoiceModel(value, value)).ToList();
        if (selected is not null && !values.Contains(selected))
        {
            choices.Add(new DashboardChoiceModel(selected, selected));
        }

        return [new DashboardChoiceModel(null, Text["DashboardAll"]), .. choices];
    }

    private void Notify()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
