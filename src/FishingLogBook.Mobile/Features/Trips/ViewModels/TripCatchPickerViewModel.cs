using System.Collections.ObjectModel;
using System.ComponentModel;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Shared.Measurements;

namespace FishingLogBook.Mobile.Features.Trips.ViewModels;

/// <summary>The Web "Add catches" modal: the angler's own unassigned catches from the trip window, multi-select.</summary>
public sealed class TripCatchPickerViewModel : INotifyPropertyChanged
{
    private readonly ITripCatchService _tripCatches;
    private readonly IAccountUnitsService _units;
    private readonly IMeasurementService _measurement;
    private readonly TimeProvider _clock;
    private LocalTripRecordModel? _trip;
    private MeasurementUnitsModel _unitsModel = MeasurementUnitsModel.Default;
    private bool _isLoading = true;
    private bool _isSaving;
    private string? _messageKey;
    private int _loadVersion;

    public TripCatchPickerViewModel(
        ITripCatchService tripCatches,
        IAccountUnitsService units,
        IMeasurementService measurement,
        UiTextService text,
        TimeProvider clock)
    {
        _tripCatches = tripCatches;
        _units = units;
        _measurement = measurement;
        _clock = clock;
        Text = text;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UiTextService Text { get; }

    public ObservableCollection<CatchChipOptionModel> Options { get; } = [];

    public bool IsLoading => _isLoading;

    public bool IsEmpty => !_isLoading && Options.Count == 0 && _messageKey is null;

    public bool ShowOptions => !_isLoading && Options.Count > 0;

    public bool HasMessage => _messageKey is not null;

    public string MessageText => _messageKey is null ? string.Empty : Text[_messageKey];

    public int SelectedCount => Options.Count(option => option.IsSelected);

    public bool CanConfirm => !_isLoading && !_isSaving && SelectedCount > 0;

    public string ConfirmText => SelectedCount == 1
        ? Text["TripAddCatchesConfirmOne"]
        : string.Format(Text.Culture, Text["TripAddCatchesConfirmMany"], SelectedCount);

    public async Task InitializeAsync(LocalTripRecordModel trip, CancellationToken cancellationToken)
    {
        var version = ++_loadVersion;
        _trip = trip;
        _isLoading = true;
        _messageKey = null;
        Options.Clear();
        Notify();
        try
        {
            _unitsModel = await _units.GetAsync(cancellationToken);
            var eligible = await _tripCatches.GetEligibleAsync(trip, cancellationToken);
            if (version != _loadVersion)
            {
                return;
            }

            foreach (var candidate in eligible)
            {
                Options.Add(new CatchChipOptionModel(candidate.Id.ToString("D"), Label(candidate)));
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (version == _loadVersion)
            {
                Options.Clear();
                _messageKey = "TripAddCatchesLoadFailed";
            }
        }
        finally
        {
            if (version == _loadVersion)
            {
                _isLoading = false;
                Notify();
            }
        }
    }

    public void Toggle(string code)
    {
        var index = Options.ToList().FindIndex(option => option.Code == code);
        if (index < 0 || _isSaving)
        {
            return;
        }

        Options[index] = Options[index] with { IsSelected = !Options[index].IsSelected };
        _messageKey = null;
        Notify();
    }

    /// <summary>Adds the selection. Returns true when at least one catch joined and the popup can close.</summary>
    public async Task<bool> ConfirmAsync(CancellationToken cancellationToken)
    {
        if (!CanConfirm || _trip is not { } trip)
        {
            return false;
        }

        var selected = Options.Where(option => option.IsSelected).Select(option => Guid.Parse(option.Code)).ToArray();
        _isSaving = true;
        _messageKey = null;
        Notify();
        TripCatchChangeModel? change = null;
        try
        {
            change = await _tripCatches.AssociateAsync(trip, selected, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _messageKey = "TripAddCatchesFailed";
        }
        finally
        {
            _isSaving = false;
            Notify();
        }

        if (change is null)
        {
            return false;
        }

        if (change.ChangedCatchIds.Count > 0)
        {
            return true;
        }

        await InitializeAsync(trip, cancellationToken);
        _messageKey = "TripAddCatchesRejected";
        Notify();
        return false;
    }

    private string Label(TripCatchCandidateModel candidate)
    {
        var local = TimeZoneInfo.ConvertTime(candidate.CaughtOn, _clock.LocalTimeZone).DateTime;
        var parts = new List<string>
        {
            local.ToString("g", Text.Culture),
            string.IsNullOrWhiteSpace(candidate.SpeciesName) ? Text["TripTimelineCatchUnknownSpecies"] : candidate.SpeciesName
        };
        if (_measurement.ToDisplayWeight(candidate.Weight, _unitsModel.WeightUnit) is { } weight)
        {
            parts.Add($"{weight.ToString("0.##", Text.Culture)} {Text[_unitsModel.WeightUnit == WeightUnitEnum.Lb ? "MeasureUnitLb" : "MeasureUnitKg"]}");
        }

        if (_measurement.ToDisplayLength(candidate.Length, _unitsModel.LengthUnit) is { } length)
        {
            parts.Add($"{length.ToString("0.##", Text.Culture)} {Text[_unitsModel.LengthUnit == LengthUnitEnum.In ? "MeasureUnitIn" : "MeasureUnitCm"]}");
        }

        return string.Join(" · ", parts);
    }

    private void Notify()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
