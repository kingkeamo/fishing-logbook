using System.Globalization;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using TripNotesComponent = FishingLogBook.Web.Features.Trips.Components.TripNotes.TripNotes;

namespace FishingLogBook.Web.Features.Trips.Components.TripEditor;

public partial class TripEditor : ComponentBase
{
    private TripNotesComponent? _notes;
    private string? _title;
    private string? _placeName;
    private Guid _loadedTripId;
    private bool _isSaving;
    private bool _saveFailed;

    [Parameter]
    [EditorRequired]
    public TripModel Trip { get; set; } = default!;

    [Parameter]
    public IReadOnlyList<CatchModel> Catches { get; set; } = [];

    [Parameter]
    public string RecordCatchBaseHref { get; set; } = "/catches/record";

    [Parameter]
    public string? SummaryLabel { get; set; }

    [Parameter]
    public WeightUnitEnum WeightUnit { get; set; } = WeightUnitEnum.Kg;

    [Parameter]
    public LengthUnitEnum LengthUnit { get; set; } = LengthUnitEnum.Cm;

    [Parameter]
    public EventCallback OnContentChanged { get; set; }

    [Parameter]
    public EventCallback OnClosed { get; set; }

    [Inject]
    private IActiveTripService ActiveTrip { get; set; } = default!;

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private IMeasurementService Measurement { get; set; } = default!;

    [Inject]
    private IModalService ModalService { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override void OnParametersSet()
    {
        if (_loadedTripId == Trip.Id)
        {
            return;
        }

        _loadedTripId = Trip.Id;
        _title = Trip.Title;
        _placeName = Trip.PlaceName;
    }

    private void OnPlaceNameChanged(string? placeName)
    {
        _placeName = placeName;
    }

    private async Task SaveAsync()
    {
        if (_isSaving)
        {
            return;
        }

        _isSaving = true;
        _saveFailed = false;
        try
        {
            var updated = await ActiveTrip.UpdateDetailsAsync(
                Trip,
                _title,
                _placeName,
                CancellationToken.None);
            if (updated is null)
            {
                _saveFailed = true;
                return;
            }

            await OnClosed.InvokeAsync();
        }
        catch (Exception exception)
        {
            _saveFailed = true;
            await Logging.LogErrorAsync("saving trip details", exception, CancellationToken.None);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task CancelAsync()
    {
        await OnClosed.InvokeAsync();
    }

    private async Task RemoveCatchAsync(CatchModel associated)
    {
        var confirmed = await ModalService.ConfirmAsync(
            new ConfirmModalModel(
                Loc["Trip_CatchRemoveTitle"].Value,
                string.Format(Loc["Trip_CatchRemoveMessage"], SpeciesLabel(associated)),
                Loc["Trip_CatchRemoveConfirm"].Value,
                Loc["Modal_Cancel"].Value),
            CancellationToken.None);
        if (!confirmed)
        {
            return;
        }

        try
        {
            await CatchStore.UpdateTripAsync(
                Trip.OwnerUserId,
                associated.Id,
                null,
                CancellationToken.None);
            await OnContentChanged.InvokeAsync();
        }
        catch (Exception exception)
        {
            _saveFailed = true;
            await Logging.LogErrorAsync("removing a catch from a trip", exception, CancellationToken.None);
        }
    }

    private string SpeciesLabel(CatchModel associated)
    {
        return string.IsNullOrWhiteSpace(associated.SpeciesName)
            ? Loc["Trip_Timeline_CatchUnknownSpecies"]
            : associated.SpeciesName!;
    }

    private string RemoveCatchLabel(CatchModel associated)
    {
        return string.Format(Loc["Trip_CatchRemoveFromTripFor"], SpeciesLabel(associated));
    }

    private string? MeasurementsLabel(CatchModel associated)
    {
        var weight = Measurement.ToDisplayWeight(associated.Weight, WeightUnit);
        var length = Measurement.ToDisplayLength(associated.Length, LengthUnit);
        var weightText = weight is null
            ? null
            : $"{weight.Value.ToString("0.##", CultureInfo.CurrentCulture)} {WeightUnitLabel}";
        var lengthText = length is null
            ? null
            : $"{length.Value.ToString("0.##", CultureInfo.CurrentCulture)} {LengthUnitLabel}";
        if (weightText is null && lengthText is null)
        {
            return null;
        }

        return string.Join(" · ", new[] { weightText, lengthText }.Where(value => value is not null));
    }

    private string WeightUnitLabel => WeightUnit == WeightUnitEnum.Lb
        ? Loc["Catch_WeightUnitShort_Lb"]
        : Loc["Catch_WeightUnitShort_Kg"];

    private string LengthUnitLabel => LengthUnit == LengthUnitEnum.In
        ? Loc["Catch_LengthUnitShort_In"]
        : Loc["Catch_LengthUnitShort_Cm"];
}
