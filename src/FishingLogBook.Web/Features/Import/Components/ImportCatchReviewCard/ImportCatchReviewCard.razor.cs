using System.Globalization;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Photographs.Models;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Features.Import.Components.ImportCatchReviewCard;

public partial class ImportCatchReviewCard : ComponentBase, IDisposable
{
    private const int MaxChipOptions = 6;
    private readonly HashSet<Guid> _selectedPhotoIds = [];
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private static readonly IReadOnlyList<TimeSpan> UtcOffsetOptions =
        [.. Enumerable.Range(0, 53).Select(index => TimeSpan.FromMinutes(-720 + (index * 30)))];
    private string _caughtOnLocal = string.Empty;
    private ImportTimestampModel? _caughtOnBasis;
    private Guid? _activePhotoId;
    private bool _caughtOnInvalid;
    private bool _editing;
    private TimeSpan? _utcOffset;
    private bool _utcOffsetInvalid;

    [Parameter, EditorRequired] public ImportCatchProposalModel Proposal { get; set; } = default!;
    [Parameter, EditorRequired] public ImportBatchModel Batch { get; set; } = default!;
    [Parameter, EditorRequired] public AnglerPreferencesModel Preferences { get; set; } = default!;
    [Parameter] public int Number { get; set; }
    [Parameter] public bool Editable { get; set; }
    [Parameter] public EventCallback<IReadOnlyList<Guid>> RemovePhotos { get; set; }
    [Parameter] public EventCallback<Guid> RemoveCatch { get; set; }
    [Parameter] public EventCallback Changed { get; set; }

    [Inject] private IModalService ModalService { get; set; } = default!;
    [Inject] private IMeasurementService Measurement { get; set; } = default!;
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private IReadOnlyList<ImportSelectedPhotoModel> ProposalPhotos =>
        [.. Proposal.PhotoIds.Select(photoId => Batch.Photos.Single(photo => photo.Id == photoId))];

    private IReadOnlyList<PhotographCarouselItemModel> CarouselPhotographs =>
        [.. ProposalPhotos.Select(photo => new PhotographCarouselItemModel(
            photo.Id,
            photo.ContentType,
            RemoteUrl: photo.ThumbnailUrl))];

    private ImportSelectedPhotoModel ActivePhoto => ProposalPhotos.First(photo => photo.Id == _activePhotoId);

    private ImportTimestampModel DisplayedTimestamp => Proposal.CaughtOn;

    private string? MeasurementLabel
    {
        get
        {
            var values = new List<string>();
            var weight = Measurement.ToDisplayWeight(Proposal.Weight, Preferences.WeightUnit);
            if (weight.HasValue)
            {
                var unit = Preferences.WeightUnit == WeightUnitEnum.Lb
                    ? Loc["Catch_WeightUnitShort_Lb"]
                    : Loc["Catch_WeightUnitShort_Kg"];
                values.Add($"{weight.Value.ToString("0.##", CultureInfo.CurrentCulture)} {unit}");
            }

            var length = Measurement.ToDisplayLength(Proposal.Length, Preferences.LengthUnit);
            if (length.HasValue)
            {
                var unit = Preferences.LengthUnit == LengthUnitEnum.In
                    ? Loc["Catch_LengthUnitShort_In"]
                    : Loc["Catch_LengthUnitShort_Cm"];
                values.Add($"{length.Value.ToString("0.##", CultureInfo.CurrentCulture)} {unit}");
            }

            return values.Count == 0 ? null : string.Join(" · ", values);
        }
    }

    private IReadOnlyList<(ImportSelectedPhotoModel Photo, ImportLocationModel Location)> LocationOptions =>
        [.. ProposalPhotos.Where(photo => photo.Location.HasCanonicalCoordinates)
            .Select(photo => (photo, photo.Location))
            .DistinctBy(option => (option.Location.Latitude, option.Location.Longitude))];

    private IReadOnlyList<(ImportCatchProposalModel Proposal, int Number)> MergeTargets =>
        [.. Batch.CatchProposals.Select((proposal, index) => (Proposal: proposal, Number: index + 1))
            .Where(item => !item.Proposal.IsRemoved && item.Proposal.Id != Proposal.Id)];

    private bool CanSplit => _selectedPhotoIds.Count > 0 && _selectedPhotoIds.Count < Proposal.PhotoIds.Count;
    private bool RequiresUtcOffsetControl => _caughtOnBasis is not null
        && (_caughtOnBasis.LocalWallClock.HasValue || !_caughtOnBasis.Instant.HasValue);
    private bool ShowLocationDecision => Proposal.HasUnresolvedGpsConflict || Proposal.Location?.HasCanonicalCoordinates == true;
    private Color StatusColor => Proposal.ReviewStatus == ImportCatchReviewStatusEnum.Reviewed
        ? Color.Success : Proposal.CanBeReviewed ? Color.Info : Color.Warning;
    private string StatusLabel => Proposal.ReviewStatus == ImportCatchReviewStatusEnum.Reviewed
        ? Loc["Import_Reviewed"] : Proposal.CanBeReviewed ? Loc["Import_Ready"] : Loc["Import_NeedsCorrection"];

    private IReadOnlyList<CatalogueOptionModel> MethodOptions => BuildShortlist(
        Preferences.Preferences.Methods.OrderByDescending(method => method.IsDefault)
            .Select(method => FindMethod(method.FishingMethodId)).Where(method => method is not null)
            .Select(method => Option(method!)), Option(Proposal.Method));

    private IReadOnlyList<CatalogueOptionModel> SpeciesOptions
    {
        get
        {
            var preferred = Preferences.Preferences.Methods
                .FirstOrDefault(method => method.FishingMethodId == Proposal.Method.Id)?.Species ?? [];
            return BuildShortlist(preferred.OrderByDescending(species => species.IsDefault)
                .Select(species => FindSpecies(species.SpeciesId)).Where(species => species is not null)
                .Select(species => Option(species!)), Option(Proposal.Species));
        }
    }

    private string TimestampLabel => DisplayedTimestamp.State switch
    {
        ImportTimestampStateEnum.ExplicitInstant => DisplayedTimestamp.Instant!.Value.ToString("dd MMM yyyy · HH:mm zzz"),
        ImportTimestampStateEnum.UserConfirmed when DisplayedTimestamp.Instant is { } instant => instant.ToString("dd MMM yyyy · HH:mm zzz"),
        ImportTimestampStateEnum.UserConfirmed => DisplayedTimestamp.LocalWallClock!.Value.ToString("dd MMM yyyy · HH:mm"),
        ImportTimestampStateEnum.LocalWallClock => Loc["Import_TimestampAmbiguous", DisplayedTimestamp.LocalWallClock!.Value.ToString("dd MMM yyyy · HH:mm")],
        ImportTimestampStateEnum.WeakFallback => Loc["Import_TimestampWeak", DisplayedTimestamp.Instant!.Value.ToString("dd MMM yyyy · HH:mm zzz")],
        ImportTimestampStateEnum.Unusable => Loc["Import_TimestampUnusable"],
        _ => Loc["Import_TimestampMissing"]
    };

    private string LocationLabel => Proposal.HasUnresolvedGpsConflict ? Loc["Import_LocationNeedsReview"]
        : Proposal.Location?.Decision == ImportLocationDecisionEnum.Accepted ? Loc["Import_LocationAccepted"]
        : Proposal.Location?.Decision == ImportLocationDecisionEnum.Removed ? Loc["Import_LocationRemoved"]
        : Proposal.Location?.HasCanonicalCoordinates == true ? Loc["Import_LocationAvailable"]
        : Loc["Import_LocationUnavailable"];

    protected override void OnParametersSet()
    {
        if (_activePhotoId is null || ProposalPhotos.All(photo => photo.Id != _activePhotoId))
        {
            _activePhotoId = ProposalPhotos[0].Id;
        }

        if (!_editing)
        {
            _caughtOnLocal = EditorValue(DisplayedTimestamp);
        }
    }
    private void OpenEditor()
    {
        _editing = true;
        _caughtOnBasis = DisplayedTimestamp;
        _caughtOnLocal = EditorValue(DisplayedTimestamp);
        _utcOffset = DisplayedTimestamp.LocalWallClock.HasValue ? DisplayedTimestamp.Instant?.Offset : null;
        _utcOffsetInvalid = false;
    }
    private void CloseEditor()
    {
        _selectedPhotoIds.Clear();
        _editing = false;
    }
    private void SetCaughtOnLocal(string value)
    {
        _caughtOnLocal = value;
        _caughtOnInvalid = false;
        _utcOffsetInvalid = false;
        var caughtOn = !TryParseCaughtOn(value, out var parsed)
            ? ImportTimestampModel.Missing()
            : RequiresUtcOffsetControl
                ? (_caughtOnBasis ?? Proposal.CaughtOn).EditLocalWallClock(parsed)
                : ConfirmedCaughtOn(parsed);
        if (RequiresUtcOffsetControl)
        {
            _utcOffset = null;
        }
        Batch.SetCatchCaughtOn(Proposal.Id, caughtOn);
    }
    private void SelectPhotos(IReadOnlySet<Guid> selectedPhotoIds)
    {
        _selectedPhotoIds.Clear();
        _selectedPhotoIds.UnionWith(selectedPhotoIds);
    }

    private Task SelectActivePhotoAsync(Guid? photoId)
    {
        if (photoId is null || ProposalPhotos.All(photo => photo.Id != photoId))
        {
            return Task.CompletedTask;
        }

        _activePhotoId = photoId;
        Batch.SetCatchCaughtOn(Proposal.Id, ActivePhoto.Timestamp);
        _caughtOnBasis = DisplayedTimestamp;
        _caughtOnLocal = EditorValue(DisplayedTimestamp);
        _caughtOnInvalid = false;
        _utcOffset = DisplayedTimestamp.LocalWallClock.HasValue ? DisplayedTimestamp.Instant?.Offset : null;
        _utcOffsetInvalid = false;
        return Task.CompletedTask;
    }

    private void ConfirmCaughtOn()
    {
        if (!TryConfirmCaughtOn())
        {
            return;
        }
    }

    private void SetUtcOffset(TimeSpan? utcOffset)
    {
        _utcOffset = utcOffset;
        _utcOffsetInvalid = false;
    }

    private static bool TryParseCaughtOn(string value, out DateTime caughtOn)
    {
        return DateTime.TryParseExact(
                value,
                "yyyy-MM-ddTHH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out caughtOn)
            || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out caughtOn)
            || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out caughtOn);
    }

    private void AcceptShownValues() => Proposal.ConfirmCanonicalValues();

    private void SelectMethod(CatalogueOptionModel method) => Batch.SetCatchMethod(Proposal.Id, Selection(method));
    private void SelectSpecies(CatalogueOptionModel species) => Batch.SetCatchSpecies(Proposal.Id, Selection(species));
    private void SetWeight(decimal? weight) => Batch.SetCatchWeight(Proposal.Id, weight);
    private void SetLength(decimal? length) => Batch.SetCatchLength(Proposal.Id, length);

    private async Task ChooseMethodAsync()
    {
        var chosen = await ChooseAsync(Loc["Import_FishingMethod"], Preferences.Catalogue.Methods.Select(Option).ToArray());
        if (chosen is not null) SelectMethod(chosen);
    }

    private async Task ChooseSpeciesAsync()
    {
        var chosen = await ChooseAsync(Loc["Import_Species"], Preferences.Catalogue.AllSpecies.Select(Option).ToArray());
        if (chosen is not null) SelectSpecies(chosen);
    }

    private async Task<CatalogueOptionModel?> ChooseAsync(string title, IReadOnlyList<CatalogueOptionModel> options)
    {
        var result = await ModalService.ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
            new CataloguePickerModalModel(title, options), _cancellationTokenSource.Token);
        return result?.Options.SingleOrDefault();
    }

    private void AcceptLocation(ImportLocationModel location) => Batch.SetCatchLocation(Proposal.Id, location.Accept());
    private void RemoveLocation() => Batch.SetCatchLocation(
        Proposal.Id,
        Proposal.Location?.Remove()
            ?? new ImportLocationModel(null, null, false, ImportLocationDecisionEnum.Removed));

    private async Task SplitSelectedAsync()
    {
        Batch.SplitCatch(Proposal.Id, _selectedPhotoIds, Guid.NewGuid());
        _selectedPhotoIds.Clear();
        await Changed.InvokeAsync();
    }

    private async Task MergeAsync(ImportCatchProposalModel target) { Batch.MergeCatches(Proposal.Id, target.Id); await Changed.InvokeAsync(); }
    private async Task RemoveSelectedAsync() { var selected = _selectedPhotoIds.ToArray(); _selectedPhotoIds.Clear(); await RemovePhotos.InvokeAsync(selected); }
    private async Task RemoveCatchAsync()
    {
        var confirmed = await ModalService.ConfirmAsync(
            new ConfirmModalModel(
                Loc["Import_RemoveCatchTitle"],
                Loc["Import_RemoveCatchMessage"],
                Loc["Import_RemoveCatchConfirm"],
                Loc["Modal_Cancel"],
                true),
            _cancellationTokenSource.Token);
        if (confirmed)
        {
            await RemoveCatch.InvokeAsync(Proposal.Id);
        }
    }

    private async Task ConfirmAsync()
    {
        Batch.ConfirmDisplayedCatch(Proposal.Id);
        await Changed.InvokeAsync();
    }

    private async Task ContinueAsync()
    {
        if (!TryConfirmCaughtOn())
        {
            return;
        }
        if (!Proposal.CanConfirmDisplayedValues)
        {
            return;
        }

        Batch.ConfirmDisplayedCatch(Proposal.Id);
        _selectedPhotoIds.Clear();
        _editing = false;
        _caughtOnInvalid = false;
        await Changed.InvokeAsync();
    }

    private static string EditorValue(ImportTimestampModel timestamp)
    {
        var value = timestamp.Instant?.DateTime ?? timestamp.LocalWallClock;
        return value?.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private ImportTimestampModel ConfirmedCaughtOn(DateTime caughtOn)
    {
        return (_caughtOnBasis ?? Proposal.CaughtOn).Confirm(caughtOn);
    }

    private bool TryConfirmCaughtOn()
    {
        if (!TryParseCaughtOn(_caughtOnLocal, out var caughtOn))
        {
            _caughtOnInvalid = true;
            return false;
        }

        if (RequiresUtcOffsetControl && !_utcOffset.HasValue)
        {
            _utcOffsetInvalid = true;
            return false;
        }

        var confirmed = RequiresUtcOffsetControl
            ? (_caughtOnBasis ?? Proposal.CaughtOn).ConfirmLocalWallClock(caughtOn, _utcOffset!.Value)
            : ConfirmedCaughtOn(caughtOn);
        Batch.SetCatchCaughtOn(Proposal.Id, confirmed);
        _caughtOnInvalid = false;
        _utcOffsetInvalid = false;
        return true;
    }

    private static string UtcOffsetLabel(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? '-' : '+';
        var absolute = offset.Duration();
        return $"UTC{sign}{absolute.Hours:D2}:{absolute.Minutes:D2}";
    }

    private FishingMethodDto? FindMethod(Guid id) => Preferences.Catalogue.Methods.SingleOrDefault(method => method.Id == id);
    private SpeciesDto? FindSpecies(Guid id) => Preferences.Catalogue.AllSpecies.SingleOrDefault(species => species.Id == id);
    private static CatalogueOptionModel Option(FishingMethodDto value) => new(value.Id, value.Code, value.Name);
    private static CatalogueOptionModel Option(SpeciesDto value) => new(value.Id, value.Code, value.Name);
    private static CatalogueOptionModel Option(ImportCatalogueSelectionModel value) => new(value.Id, value.Code, value.Name);
    private static ImportCatalogueSelectionModel Selection(CatalogueOptionModel value) => new(value.Id, value.Code, value.Name);

    private static IReadOnlyList<CatalogueOptionModel> BuildShortlist(IEnumerable<CatalogueOptionModel> options, CatalogueOptionModel selected)
    {
        var shortlist = options.Take(MaxChipOptions).ToList();
        if (shortlist.All(option => option.Id != selected.Id)) shortlist.Insert(0, selected);
        return shortlist;
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
