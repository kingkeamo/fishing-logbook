using System.Globalization;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Photographs.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Features.Catch.Components.CatchCard;

public partial class CatchCard : ComponentBase
{
    [Parameter, EditorRequired]
    public CatchModel Catch { get; set; } = default!;

    [Parameter]
    public DateTime LocalCaughtOn { get; set; }

    [Parameter]
    public DateTime LocalToday { get; set; }

    [Parameter]
    public WeightUnitEnum WeightUnit { get; set; } = WeightUnitEnum.Kg;

    [Parameter]
    public LengthUnitEnum LengthUnit { get; set; } = LengthUnitEnum.Cm;

    [Parameter]
    public Guid CurrentUserId { get; set; }

    [Parameter]
    public bool IsRetrying { get; set; }

    [Parameter]
    public EventCallback<Guid> OnRetry { get; set; }

    [Parameter]
    public EventCallback<Guid> OnLocationPrivacy { get; set; }

    [Parameter]
    public string EditHrefPrefix { get; set; } = "/catches";

    [Parameter]
    public bool ShowOnlineActions { get; set; } = true;

    [Parameter]
    public string? AnglerName { get; set; }

    [Parameter]
    public string? RecordedByName { get; set; }

    [Inject]
    private IMeasurementService Measurement { get; set; } = default!;

    [Inject]
    private ICatchDateGroupingService DateGrouping { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private IReadOnlyList<PhotographCarouselItemModel> CarouselPhotographs =>
        Catch.Photographs
            .Select(photograph => new PhotographCarouselItemModel(
                photograph.Id,
                photograph.ContentType,
                photograph.Bytes,
                photograph.RemoteUrl))
            .ToArray();

    private string EditHref => $"{EditHrefPrefix}/{Catch.Id:D}/edit";

    private string SpeciesLabel => string.IsNullOrWhiteSpace(Catch.SpeciesName)
        ? Loc["Catch_UnknownSpecies"]
        : Catch.SpeciesName;

    private string DateTimeLabel =>
        $"{DateGrouping.RelativeDayLabel(LocalCaughtOn, LocalToday)} · {LocalCaughtOn.ToString("t", CultureInfo.CurrentCulture)}";

    private bool HasMethod => !string.IsNullOrWhiteSpace(Catch.Method);

    private bool HasBaitOrLure => !string.IsNullOrWhiteSpace(Catch.BaitOrLure);

    private bool HasNotes => !string.IsNullOrWhiteSpace(Catch.Notes);

    private string? MeasurementsLabel
    {
        get
        {
            var weight = Measurement.ToDisplayWeight(Catch.Weight, WeightUnit);
            var length = Measurement.ToDisplayLength(Catch.Length, LengthUnit);

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

            return string.Join(
                " · ",
                new[] { weightText, lengthText }
                    .Where(value => value is not null));
        }
    }

    private string WeightUnitLabel => WeightUnit == WeightUnitEnum.Lb
        ? Loc["Catch_WeightUnitShort_Lb"]
        : Loc["Catch_WeightUnitShort_Kg"];

    private string LengthUnitLabel => LengthUnit == LengthUnitEnum.In
        ? Loc["Catch_LengthUnitShort_In"]
        : Loc["Catch_LengthUnitShort_Cm"];

    private string? ProvenanceLabel
    {
        get
        {
            if (Catch.AnglerUserId != Guid.Empty &&
                Catch.AnglerUserId != CurrentUserId)
            {
                return string.IsNullOrWhiteSpace(AnglerName)
                    ? Loc["Catch_RecordedForAnotherAngler"].Value
                    : Loc["Catch_RecordedFor", AnglerName].Value;
            }

            if (Catch.RecordedByUserId != Guid.Empty &&
                Catch.RecordedByUserId != CurrentUserId)
            {
                return string.IsNullOrWhiteSpace(RecordedByName)
                    ? Loc["Catch_RecordedBySomeoneElse"].Value
                    : Loc["Catch_RecordedByName", RecordedByName].Value;
            }

            return null;
        }
    }

    private bool ShowsAttentionBanner =>
        Catch.SyncStatus is SyncStatus.SavedLocally
            or SyncStatus.WaitingToSynchronise
            or SyncStatus.FailedToSynchronise;

    private bool IsSynchronising =>
        Catch.SyncStatus == SyncStatus.Synchronising;

    private bool HasFailed =>
        Catch.SyncStatus == SyncStatus.FailedToSynchronise;

    private Severity AttentionSeverity =>
        HasFailed
            ? Severity.Warning
            : Severity.Info;

    private string AttentionMessage =>
        HasFailed
            ? Loc["Catch_SyncFailureReassurance"]
            : Loc["Catch_SyncPendingReassurance"];

    private string RetryLabel =>
        HasFailed
            ? Loc["Catch_SyncRetry"]
            : Loc["Catch_SyncNow"];

    private Task RetryAsync()
    {
        return OnRetry.InvokeAsync(Catch.Id);
    }

    private Task LocationPrivacyAsync()
    {
        return OnLocationPrivacy.InvokeAsync(Catch.Id);
    }
}
