using System.Globalization;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Modals.LocationPrivacy;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Pages.CatchEdit;

public partial class CatchEdit : ComponentBase, IDisposable
{
    private const int MaxChipOptions = 6;
    private const long MaxPhotographBytes = 10 * 1024 * 1024;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private CatchModel? _catch;
    private string _speciesName = string.Empty;
    private string _weightText = string.Empty;
    private string _lengthText = string.Empty;
    private string _method = string.Empty;
    private string _baitOrLure = string.Empty;
    private string _notes = string.Empty;
    private string _caughtOnLocal = string.Empty;
    private string? _validationMessage;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _loadFailed;
    private bool _offlineUnavailable;
    private bool _saveFailed;
    private bool _saved;
    private bool _unsupportedFormat;
    private bool _cannotRemoveLastPhoto;
    private bool _addPhotoFailed;
    private bool _removePhotoFailed;
    private WeightUnitEnum _weightUnit = WeightUnitEnum.Kg;
    private LengthUnitEnum _lengthUnit = LengthUnitEnum.Cm;
    private bool _catalogueUnavailable;
    private bool _speciesIsExplicit;
    private FishingPreferencesDto? _preferences;
    private IReadOnlyList<FishingMethodDto> _catalogueMethods = [];
    private IReadOnlyList<SpeciesDto> _catalogueSpecies = [];

    [Parameter]
    public Guid CatchId { get; set; }

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private ICatchClient CatchClient { get; set; } = default!;

    [Inject]
    private ILocalCatchOwnerService LocalCatchOwner { get; set; } = default!;

    [Inject]
    private ICatchSynchroniser CatchSynchroniser { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    [Inject]
    private ITimeService Time { get; set; } = default!;

    [Inject]
    private IAnglerPreferencesProvider AnglerPreferences { get; set; } = default!;

    [Inject]
    private IMeasurementService Measurement { get; set; } = default!;

    [Inject]
    private IModalService ModalService { get; set; } = default!;

    private string WeightUnitLabel
    {
        get
        {
            return _weightUnit == WeightUnitEnum.Lb
                ? Loc["Catch_WeightUnitShort_Lb"]
                : Loc["Catch_WeightUnitShort_Kg"];
        }
    }

    private string LengthUnitLabel
    {
        get
        {
            return _lengthUnit == LengthUnitEnum.In
                ? Loc["Catch_LengthUnitShort_In"]
                : Loc["Catch_LengthUnitShort_Cm"];
        }
    }

    private string WeightLabel
    {
        get
        {
            return $"{Loc["Catch_EditWeight"]} ({WeightUnitLabel})";
        }
    }

    private string LengthLabel
    {
        get
        {
            return $"{Loc["Catch_EditLength"]} ({LengthUnitLabel})";
        }
    }

    private string WeightInvalidMessage
    {
        get
        {
            return Loc["Catch_EditWeightInvalid", WeightUnitLabel, Measurement.MaxDisplayWeight(_weightUnit)];
        }
    }

    private string LengthInvalidMessage
    {
        get
        {
            return Loc["Catch_EditLengthInvalid", LengthUnitLabel, Measurement.MaxDisplayLength(_lengthUnit)];
        }
    }

    private IReadOnlyList<CatchPhotographCarouselItemModel> CarouselPhotographs
    {
        get
        {
            return _catch is null
                ? []
                : _catch.Photographs
                    .Where(photograph => photograph.SyncStatus != SyncStatus.PendingDeletion)
                    .Select(photograph => new CatchPhotographCarouselItemModel(
                        photograph.Id,
                        photograph.ContentType,
                        photograph.Bytes,
                        photograph.RemoteUrl))
                    .ToArray();
        }
    }

    private string? LocationVisibilityLabel
    {
        get
        {
            return _catch?.Location?.Visibility switch
            {
                LocationDefaults.Private => Loc["Catch_LocationVisibilityPrivate"].Value,
                LocationDefaults.Approximate => Loc["Catch_LocationVisibilityApproximate"].Value,
                LocationDefaults.FishingVenueOnly => Loc["Catch_LocationVisibilityFishingVenueOnly"].Value,
                LocationDefaults.Public => Loc["Catch_LocationVisibilityPublic"].Value,
                _ => null
            };
        }
    }

    private IReadOnlyList<CatchChipOptionModel> MethodOptions
    {
        get
        {
            var preferred = _preferences?.Methods
                .OrderByDescending(method => method.IsDefault)
                .Select(method => new CatchChipOptionModel(method.Code, method.Name))
                .ToArray() ?? [];
            var options = preferred.Length > 0
                ? preferred
                : [.. _catalogueMethods.Select(method => new CatchChipOptionModel(method.Code, method.Name))];
            return CatchChipOptionModel.BuildShortlist(options, _method, MaxChipOptions);
        }
    }

    private IReadOnlyList<CatchChipOptionModel> SpeciesOptions
    {
        get
        {
            var methodPreference = FindMethodPreference(_method);
            var preferred = methodPreference?.Species
                .OrderByDescending(species => species.IsDefault)
                .Select(species => new CatchChipOptionModel(species.Code, species.Name))
                .ToArray() ?? [];
            return CatchChipOptionModel.BuildShortlist(preferred, _speciesName, MaxChipOptions);
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadFailed = false;
        _offlineUnavailable = false;
        try
        {
            await LoadPreferencesAsync();
            var ownerUserId = await LocalCatchOwner.GetUserIdAsync(_cancellationTokenSource.Token);
            _catch = await CatchStore.GetAsync(ownerUserId, CatchId, _cancellationTokenSource.Token)
                ?? await TryLocalizeFromServerAsync(ownerUserId, _cancellationTokenSource.Token);
            if (_catch is null)
            {
                _loadFailed = true;
                return;
            }

            if (!await BindFormAsync(_catch))
            {
                _loadFailed = true;
                _catch = null;
                return;
            }

            ApplyProfileDefaultsToEmptyFields();
        }
        catch (Exception)
        {
            _loadFailed = true;
            _catch = null;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task<CatchModel?> TryLocalizeFromServerAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        CatchViewDto? remote;
        try
        {
            remote = await CatchClient.GetAsync(CatchId, cancellationToken);
        }
        catch (Exception)
        {
            _offlineUnavailable = true;
            return null;
        }

        if (remote is null || remote.UserId != ownerUserId || remote.Photographs.Count == 0)
        {
            return null;
        }

        var photographs = new List<CatchPhotographModel>();
        foreach (var photograph in remote.Photographs)
        {
            if (string.IsNullOrWhiteSpace(photograph.Url))
            {
                _offlineUnavailable = true;
                return null;
            }

            try
            {
                var bytes = await CatchClient.DownloadPhotographAsync(photograph.Url, cancellationToken);
                photographs.Add(new CatchPhotographModel(
                    photograph.Id,
                    remote.Id,
                    photograph.ContentType,
                    bytes,
                    SyncStatus.Synchronised));
            }
            catch (Exception)
            {
                _offlineUnavailable = true;
                return null;
            }
        }

        var localized = new CatchModel(
            remote.Id,
            remote.CaughtOn,
            photographs,
            remote.SpeciesName,
            ToLocationModel(remote.Location),
            remote.UserId,
            SyncStatus.Synchronised,
            SyncStatus.Synchronised,
            remote.AnglerUserId,
            remote.RecordedByUserId,
            remote.Weight,
            remote.Length,
            remote.Method,
            remote.BaitOrLure,
            remote.Notes);

        try
        {
            await CatchStore.SaveAsync(localized, cancellationToken);
        }
        catch (Exception)
        {
            _offlineUnavailable = true;
            return null;
        }

        return localized;
    }

    private static CatchLocationModel? ToLocationModel(CatchLocationExposureDto? exposure)
    {
        if (exposure is null
            || exposure.Latitude is null
            || exposure.Longitude is null
            || exposure.CapturedOn is null)
        {
            return null;
        }

        return new CatchLocationModel(
            exposure.Latitude.Value,
            exposure.Longitude.Value,
            exposure.AccuracyMetres,
            exposure.CapturedOn.Value,
            exposure.Source ?? LocationDefaults.DeviceGps,
            exposure.Visibility,
            LocationDefaults.ConsentVersion);
    }

    private async Task LoadPreferencesAsync()
    {
        var anglerPreferences = await AnglerPreferences.GetAsync(_cancellationTokenSource.Token);
        _weightUnit = anglerPreferences.WeightUnit;
        _lengthUnit = anglerPreferences.LengthUnit;
        _catalogueMethods = anglerPreferences.Catalogue.Methods;
        _catalogueSpecies = anglerPreferences.Catalogue.AllSpecies;
        _preferences = anglerPreferences.Preferences;
        _catalogueUnavailable = !anglerPreferences.HasCatalogue;
    }

    private void ApplyProfileDefaultsToEmptyFields()
    {
        if (string.IsNullOrWhiteSpace(_method))
        {
            _method = _preferences?.Methods.FirstOrDefault(method => method.IsDefault)?.Name
                ?? string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(_speciesName))
        {
            return;
        }

        _speciesName = FindMethodPreference(_method)?.Species
            .FirstOrDefault(species => species.IsDefault)?.Name
            ?? string.Empty;
    }

    private FishingMethodPreferenceDto? FindMethodPreference(string methodName)
    {
        return _preferences?.Methods.FirstOrDefault(method =>
            string.Equals(method.Name, methodName, StringComparison.OrdinalIgnoreCase));
    }

    private void SelectMethod(string method)
    {
        _method = method;
        ApplyDefaultSpeciesForMethod();
    }

    private void ApplyDefaultSpeciesForMethod()
    {
        if (_speciesIsExplicit)
        {
            return;
        }

        _speciesName = FindMethodPreference(_method)?.Species
            .FirstOrDefault(species => species.IsDefault)?.Name
            ?? string.Empty;
    }

    private void SelectSpecies(string species)
    {
        _speciesName = species;
        _speciesIsExplicit = true;
    }

    private string MethodInput
    {
        get
        {
            return _method;
        }

        set
        {
            SelectMethod(value);
        }
    }

    private string SpeciesInput
    {
        get
        {
            return _speciesName;
        }

        set
        {
            _speciesName = value;
            _speciesIsExplicit = !string.IsNullOrWhiteSpace(value);
        }
    }

    private async Task ChooseMethodAsync()
    {
        var chosen = await ChooseFromCatalogueAsync(
            Loc["Catch_EditMethod"],
            [.. _catalogueMethods.Select(method => new CatalogueOptionModel(method.Id, method.Code, method.Name))]);
        if (chosen is not null)
        {
            SelectMethod(chosen.Name);
        }
    }

    private async Task ChooseSpeciesAsync()
    {
        var chosen = await ChooseFromCatalogueAsync(
            Loc["Catch_EditSpecies"],
            [.. _catalogueSpecies.Select(species => new CatalogueOptionModel(species.Id, species.Code, species.Name))]);
        if (chosen is not null)
        {
            SelectSpecies(chosen.Name);
        }
    }

    private async Task<CatalogueOptionModel?> ChooseFromCatalogueAsync(
        string title,
        IReadOnlyList<CatalogueOptionModel> options)
    {
        var result = await ModalService.ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
            new CataloguePickerModalModel(title, options),
            _cancellationTokenSource.Token);
        return result?.Option;
    }

    private async Task OnAddPhotographsSelected(InputFileChangeEventArgs args)
    {
        if (_catch is null)
        {
            return;
        }

        _addPhotoFailed = false;
        var rejectedUnsupported = false;
        var updated = _catch;
        foreach (var file in args.GetMultipleFiles(10))
        {
            if (!PhotographContentTypeConstants.IsAllowed(file.ContentType))
            {
                rejectedUnsupported = true;
                continue;
            }

            updated = await AppendPhotographAsync(updated, file);
        }

        _unsupportedFormat = rejectedUnsupported;
        if (ReferenceEquals(updated, _catch))
        {
            return;
        }

        try
        {
            await CatchStore.SaveAsync(updated, _cancellationTokenSource.Token);
            _catch = updated;
            TryToSynchronisePending();
        }
        catch (Exception)
        {
            _addPhotoFailed = true;
        }
    }

    private async Task<CatchModel> AppendPhotographAsync(CatchModel current, IBrowserFile file)
    {
        await using var stream = file.OpenReadStream(MaxPhotographBytes);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, _cancellationTokenSource.Token);
        var photograph = new CatchPhotographModel(
            Guid.NewGuid(),
            current.Id,
            file.ContentType,
            buffer.ToArray());
        return current with
        {
            Photographs = [.. current.Photographs, photograph],
            SyncStatus = PendingOverallStatus(current.SyncStatus)
        };
    }

    private async Task OnRemovePhotographAsync(Guid photographId)
    {
        if (_catch is null)
        {
            return;
        }

        _addPhotoFailed = false;
        _removePhotoFailed = false;
        var visibleCount = _catch.Photographs
            .Count(photograph => photograph.SyncStatus != SyncStatus.PendingDeletion);
        if (visibleCount <= 1)
        {
            _cannotRemoveLastPhoto = true;
            return;
        }

        _cannotRemoveLastPhoto = false;
        var confirmed = await ModalService.ConfirmAsync(
            new ConfirmModalModel(
                Loc["Catch_EditRemovePhotoTitle"].Value,
                Loc["Catch_EditRemovePhotoMessage"].Value,
                Loc["Catch_EditRemovePhotoConfirm"].Value,
                Loc["Modal_Cancel"].Value,
                IsDestructive: true),
            _cancellationTokenSource.Token);
        if (!confirmed)
        {
            return;
        }

        var updated = _catch with
        {
            Photographs = _catch.Photographs
                .Select(photograph => photograph.Id == photographId
                    ? photograph with { SyncStatus = SyncStatus.PendingDeletion }
                    : photograph)
                .ToArray(),
            SyncStatus = PendingOverallStatus(_catch.SyncStatus)
        };
        try
        {
            await CatchStore.SaveAsync(updated, _cancellationTokenSource.Token);
            _catch = updated;
            TryToSynchronisePending();
        }
        catch (Exception)
        {
            _removePhotoFailed = true;
        }
    }

    private async Task OpenLocationPrivacyAsync()
    {
        if (_catch is null)
        {
            return;
        }

        var result = await ModalService.ShowAsync<LocationPrivacyModal, LocationPrivacyModalModel, LocationPrivacyModalResult>(
            new LocationPrivacyModalModel(CatchId),
            _cancellationTokenSource.Token);
        if (result?.Saved != true)
        {
            return;
        }

        var ownerUserId = await LocalCatchOwner.GetUserIdAsync(_cancellationTokenSource.Token);
        var reloaded = await CatchStore.GetAsync(ownerUserId, CatchId, _cancellationTokenSource.Token);
        if (reloaded is not null)
        {
            _catch = reloaded;
        }
    }

    private async Task SaveAsync()
    {
        if (_catch is null || _isSaving)
        {
            return;
        }

        _isSaving = true;
        _saveFailed = false;
        _saved = false;
        _validationMessage = null;
        try
        {
            var built = await TryBuildUpdatedCatchAsync();
            if (built is null)
            {
                return;
            }

            await CatchStore.SaveAsync(built.Updated, _cancellationTokenSource.Token);
            _catch = built.Updated;
            await BindFormAsync(built.Updated);
            _saved = true;
            if (built.MetadataChanged)
            {
                TryToSynchronisePending();
            }
        }
        catch (Exception)
        {
            _saveFailed = true;
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task<BuiltCatchEdit?> TryBuildUpdatedCatchAsync()
    {
        var details = await TryReadEditedDetailsAsync();
        if (details is null)
        {
            return null;
        }

        var metadataChanged = HasDetailsChanged(details);
        var updated = _catch! with
        {
            SpeciesName = details.SpeciesName,
            Weight = details.Weight,
            Length = details.Length,
            Method = details.Method,
            BaitOrLure = details.BaitOrLure,
            Notes = details.Notes,
            CaughtOn = details.CaughtOn,
            MetadataSyncStatus = metadataChanged
                ? SyncStatus.WaitingToSynchronise
                : _catch.MetadataSyncStatus,
            SyncStatus = metadataChanged
                ? PendingOverallStatus(_catch.SyncStatus)
                : _catch.SyncStatus
        };
        return new BuiltCatchEdit(updated, metadataChanged);
    }

    private async Task<EditedCatchDetails?> TryReadEditedDetailsAsync()
    {
        var caughtOn = await TryParseCaughtOnAsync();
        if (caughtOn is null)
        {
            _validationMessage = Loc["Catch_EditCaughtOnInvalid"];
            return null;
        }

        if (!TryParseMeasurement(_weightText, out var displayWeight))
        {
            _validationMessage = WeightInvalidMessage;
            return null;
        }

        var weight = Measurement.ToCanonicalWeight(displayWeight, _weightUnit, _catch?.Weight);
        if (!CatchDetailConstants.IsWeightValid(weight))
        {
            _validationMessage = WeightInvalidMessage;
            return null;
        }

        if (!TryParseMeasurement(_lengthText, out var displayLength))
        {
            _validationMessage = LengthInvalidMessage;
            return null;
        }

        var length = Measurement.ToCanonicalLength(displayLength, _lengthUnit, _catch?.Length);
        if (!CatchDetailConstants.IsLengthValid(length))
        {
            _validationMessage = LengthInvalidMessage;
            return null;
        }

        var speciesName = TrimToNull(_speciesName);
        var method = TrimToNull(_method);
        var baitOrLure = TrimToNull(_baitOrLure);
        var notes = TrimToNull(_notes);
        if (!CatchDetailConstants.IsOptionalTextValid(speciesName, CatchDetailConstants.MaxSpeciesNameLength)
            || !CatchDetailConstants.IsOptionalTextValid(method, CatchDetailConstants.MaxMethodLength)
            || !CatchDetailConstants.IsOptionalTextValid(baitOrLure, CatchDetailConstants.MaxBaitOrLureLength)
            || !CatchDetailConstants.IsOptionalTextValid(notes, CatchDetailConstants.MaxNotesLength))
        {
            _validationMessage = Loc["Catch_EditTextTooLong"];
            return null;
        }

        return new EditedCatchDetails(
            speciesName,
            weight,
            length,
            method,
            baitOrLure,
            notes,
            caughtOn.Value);
    }

    private bool HasDetailsChanged(EditedCatchDetails details)
    {
        return !string.Equals(_catch!.SpeciesName, details.SpeciesName, StringComparison.Ordinal)
            || _catch.Weight != details.Weight
            || _catch.Length != details.Length
            || !string.Equals(_catch.Method, details.Method, StringComparison.Ordinal)
            || !string.Equals(_catch.BaitOrLure, details.BaitOrLure, StringComparison.Ordinal)
            || !string.Equals(_catch.Notes, details.Notes, StringComparison.Ordinal)
            || _catch.CaughtOn != details.CaughtOn;
    }

    private static SyncStatus PendingOverallStatus(SyncStatus current)
    {
        if (current is SyncStatus.Synchronised
            or SyncStatus.FailedToSynchronise
            or SyncStatus.Synchronising)
        {
            return SyncStatus.WaitingToSynchronise;
        }

        return current;
    }

    private async Task<bool> BindFormAsync(CatchModel catchRecord)
    {
        _speciesName = catchRecord.SpeciesName ?? string.Empty;
        _speciesIsExplicit = !string.IsNullOrWhiteSpace(catchRecord.SpeciesName);
        var displayWeight = Measurement.ToDisplayWeight(catchRecord.Weight, _weightUnit);
        var displayLength = Measurement.ToDisplayLength(catchRecord.Length, _lengthUnit);
        _weightText = displayWeight?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _lengthText = displayLength?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _method = catchRecord.Method ?? string.Empty;
        _baitOrLure = catchRecord.BaitOrLure ?? string.Empty;
        _notes = catchRecord.Notes ?? string.Empty;
        var localValue = await Time.ToDateTimeLocalValueAsync(
            catchRecord.CaughtOn,
            _cancellationTokenSource.Token);
        if (string.IsNullOrWhiteSpace(localValue))
        {
            return false;
        }

        _caughtOnLocal = localValue;
        return true;
    }

    private async Task<DateTimeOffset?> TryParseCaughtOnAsync()
    {
        var converted = await Time.FromDateTimeLocalValueAsync(
            _caughtOnLocal,
            _cancellationTokenSource.Token);
        if (converted is null)
        {
            return null;
        }

        var caughtOn = converted.Value.ToUniversalTime();
        if (_catch is not null)
        {
            var originalLocal = await Time.ToDateTimeLocalValueAsync(
                _catch.CaughtOn,
                _cancellationTokenSource.Token);
            if (string.Equals(originalLocal, _caughtOnLocal, StringComparison.Ordinal))
            {
                caughtOn = _catch.CaughtOn.ToUniversalTime();
            }
        }

        if (!CatchDetailConstants.IsCaughtOnValid(caughtOn, DateTimeOffset.UtcNow))
        {
            return null;
        }

        return caughtOn;
    }

    private static bool TryParseMeasurement(string text, out decimal? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!decimal.TryParse(
                text,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed)
            && !decimal.TryParse(
                text,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private void TryToSynchronisePending()
    {
        _ = SafeSynchronisePendingAsync();
    }

    private async Task SafeSynchronisePendingAsync()
    {
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            await CatchSynchroniser.SynchronisePendingAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "catch synchronisation",
                exception,
                CancellationToken.None);
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }

    private sealed record EditedCatchDetails(
        string? SpeciesName,
        decimal? Weight,
        decimal? Length,
        string? Method,
        string? BaitOrLure,
        string? Notes,
        DateTimeOffset CaughtOn);

    private sealed record BuiltCatchEdit(CatchModel Updated, bool MetadataChanged);
}
