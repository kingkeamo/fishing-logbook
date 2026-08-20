using System.Globalization;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Features.Catch.Components.CatchCard;

public partial class CatchCard : ComponentBase
{
    private IReadOnlyList<Guid> _photographIds = [];
    private IReadOnlyList<string> _photoUrls = [];
    private int _currentPhotographIndex;

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

    [Inject]
    private IMeasurementService Measurement { get; set; } = default!;

    [Inject]
    private ICatchDateGroupingService DateGrouping { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override void OnParametersSet()
    {
        var photographs = Catch.Photographs
            .Where(item =>
                item.Bytes is { Length: > 0 } ||
                !string.IsNullOrWhiteSpace(item.RemoteUrl))
            .ToArray();

        var currentIds = photographs
            .Select(item => item.Id)
            .ToArray();

        if (currentIds.SequenceEqual(_photographIds))
        {
            return;
        }

        _photographIds = currentIds;
        _photoUrls = photographs
            .Select(ToPhotoUrl)
            .ToArray();

        if (_currentPhotographIndex >= _photoUrls.Count)
        {
            _currentPhotographIndex = 0;
        }
    }

    private static string ToPhotoUrl(CatchPhotographModel photograph)
    {
        if (photograph.Bytes is { Length: > 0 })
        {
            return $"data:{photograph.ContentType};base64,{Convert.ToBase64String(photograph.Bytes)}";
        }

        return photograph.RemoteUrl!;
    }

    private string EditHref => $"/catches/{Catch.Id:D}/edit";

    private string SpeciesLabel => string.IsNullOrWhiteSpace(Catch.SpeciesName)
        ? Loc["Catch_UnknownSpecies"]
        : Catch.SpeciesName;

    private string DateTimeLabel =>
        $"{DateGrouping.RelativeDayLabel(LocalCaughtOn, LocalToday)} · {LocalCaughtOn.ToString("t", CultureInfo.CurrentCulture)}";

    private bool HasMethod => !string.IsNullOrWhiteSpace(Catch.Method);

    private bool HasBaitOrLure => !string.IsNullOrWhiteSpace(Catch.BaitOrLure);

    private bool HasNotes => !string.IsNullOrWhiteSpace(Catch.Notes);

    private int PhotographCount => _photoUrls.Count;

    private bool HasMultiplePhotographs => PhotographCount > 1;

    private int CurrentPhotographNumber =>
        PhotographCount == 0
            ? 0
            : _currentPhotographIndex + 1;

    private string? CurrentPhotoUrl =>
        PhotographCount == 0
            ? null
            : _photoUrls[_currentPhotographIndex];

    private string PhotoElementId =>
        HasMultiplePhotographs
            ? $"catch-card-photo-{Catch.Id:D}-{_currentPhotographIndex}"
            : $"catch-card-photo-{Catch.Id:D}";

    private string CurrentPhotographAlt =>
        HasMultiplePhotographs
            ? Loc[
                "Catch_PhotographAltNumbered",
                CurrentPhotographNumber,
                PhotographCount]
            : Loc["Catch_PhotographAlt"];

    private void PreviousPhotograph()
    {
        if (PhotographCount <= 1)
        {
            return;
        }

        _currentPhotographIndex =
            (_currentPhotographIndex - 1 + PhotographCount) % PhotographCount;
    }

    private void NextPhotograph()
    {
        if (PhotographCount <= 1)
        {
            return;
        }

        _currentPhotographIndex =
            (_currentPhotographIndex + 1) % PhotographCount;
    }

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
                return Loc["Catch_RecordedForAnotherAngler"];
            }

            if (Catch.RecordedByUserId != Guid.Empty &&
                Catch.RecordedByUserId != CurrentUserId)
            {
                return Loc["Catch_RecordedBySomeoneElse"];
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
