using System.Globalization;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Components.RecordCatchEditor;

public partial class RecordCatchEditor : ComponentBase, IDisposable
{
    private const long MaxPhotographBytes = 10 * 1024 * 1024;
    private const int MaxSelectedPhotographs = 10;
    private const int MaxChipOptions = 6;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<PendingPhotograph> _photographs = [];
    private DateTimeOffset? _fallbackCaughtOn;
    private DateTimeOffset? _proposedCaughtOn;
    private string _caughtOnLocal = string.Empty;
    private bool _caughtOnResolvedByAngler;
    private bool _caughtOnInvalid;
    private bool _deviceLocationChosen;
    private Guid? _representativePhotographId;
    private PhotoMetadataProposalModel _proposal = PhotoMetadataProposalModel.Empty;
    private decimal? _weight;
    private decimal? _length;
    private Guid? _activePhotographId;
    private string _selectedMethod = string.Empty;
    private string _selectedSpecies = string.Empty;
    private bool _speciesIsExplicit;
    private string? _carriedMethod;
    private string? _carriedSpecies;
    private bool _carriedSpeciesWasExplicit;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _isSaved;
    private bool _saveFailed;
    private bool _unsupportedFormat;
    private bool _unpreparablePhotograph;
    private bool _catalogueUnavailable;
    private bool _locationCaptureStarted;
    private bool _locationSaved;
    private LocationPromptStatus _locationPrompt = new(false, false, false);
    private CatchLocationModel? _capturedLocation;
    private FishingPreferencesDto? _preferences;
    private IReadOnlyList<FishingMethodDto> _catalogueMethods = [];
    private IReadOnlyList<SpeciesDto> _catalogueSpecies = [];

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private ILocationService LocationService { get; set; } = default!;

    [Inject]
    private ITimeService Time { get; set; } = default!;

    [Inject]
    private IPhotoMetadataService PhotoMetadata { get; set; } = default!;

    [Inject]
    private IPhotoMetadataProposalService PhotoMetadataProposal { get; set; } = default!;

    [Inject]
    private IModalService ModalService { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    [Parameter] public Guid OwnerUserId { get; set; }
    [Parameter] public AnglerPreferencesModel Preferences { get; set; } = AnglerPreferencesModel.Empty;
    [Parameter] public string ViewCatchesHref { get; set; } = "/catches";
    [Parameter] public EventCallback Saved { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var locationPrompt = RefreshLocationPromptAsync();
            await locationPrompt;
            LoadCatalogue();
            ApplyProfileDefaults();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void LoadCatalogue()
    {
        _catalogueMethods = Preferences.Catalogue.Methods;
        _catalogueSpecies = Preferences.Catalogue.AllSpecies;
        _preferences = Preferences.Preferences;
        _catalogueUnavailable = !Preferences.HasCatalogue;
    }

    private void ApplyProfileDefaults()
    {
        var defaultMethod = _preferences?.Methods.FirstOrDefault(method => method.IsDefault);
        if (defaultMethod is null)
        {
            return;
        }

        _selectedMethod = defaultMethod.Name;
        _selectedSpecies = defaultMethod.Species.FirstOrDefault(species => species.IsDefault)?.Name
            ?? string.Empty;
        _speciesIsExplicit = false;
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
            return CatchChipOptionModel.BuildShortlist(options, _selectedMethod, MaxChipOptions);
        }
    }

    private IReadOnlyList<CatchChipOptionModel> SpeciesOptions
    {
        get
        {
            var methodPreference = FindMethodPreference(_selectedMethod);
            var preferred = methodPreference?.Species
                .OrderByDescending(species => species.IsDefault)
                .Select(species => new CatchChipOptionModel(species.Code, species.Name))
                .ToArray() ?? [];
            return CatchChipOptionModel.BuildShortlist(preferred, _selectedSpecies, MaxChipOptions);
        }
    }

    private FishingMethodPreferenceDto? FindMethodPreference(string methodName)
    {
        return _preferences?.Methods.FirstOrDefault(method =>
            string.Equals(method.Name, methodName, StringComparison.OrdinalIgnoreCase));
    }

    private void SelectMethod(string method)
    {
        _selectedMethod = method;
        ApplyDefaultSpeciesForMethod();
    }

    private void ApplyDefaultSpeciesForMethod()
    {
        if (_speciesIsExplicit)
        {
            return;
        }

        _selectedSpecies = FindMethodPreference(_selectedMethod)?.Species
            .FirstOrDefault(species => species.IsDefault)?.Name
            ?? string.Empty;
    }

    private void SelectSpecies(string species)
    {
        _selectedSpecies = species;
        _speciesIsExplicit = true;
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
        return result?.Options.SingleOrDefault();
    }

    private bool CanSave
    {
        get
        {
            return !_isSaved && _photographs.Count > 0 && !_isSaving && !DateConflict;
        }
    }

    private bool DateConflict =>
        _proposal.HasConflictingDates
        && !_caughtOnResolvedByAngler
        && _representativePhotographId is null;

    private bool CoordinateConflict =>
        _proposal.HasConflictingCoordinates && _representativePhotographId is null;

    private bool MetadataConflict => DateConflict || CoordinateConflict;

    private bool ShowPhotographChooser =>
        _proposal.HasConflictingDates || _proposal.HasConflictingCoordinates;

    private bool CurrentPhotographIsRepresentative =>
        _representativePhotographId is { } photographId
        && CurrentPhotograph?.Id == photographId;

    private bool PhotoLocationApplied =>
        _capturedLocation is not null
        && string.Equals(
            _capturedLocation.Source,
            LocationDefaults.PhotoMetadata,
            StringComparison.Ordinal);

    private bool DeviceLocationApplied =>
        _capturedLocation is not null
        && string.Equals(
            _capturedLocation.Source,
            LocationDefaults.DeviceGps,
            StringComparison.Ordinal);

    private PendingPhotograph? CurrentPhotograph =>
        _photographs.Count == 0
            ? null
            : _photographs.FirstOrDefault(photograph => photograph.Id == _activePhotographId)
                ?? _photographs[0];

    private PendingPhotograph? RepresentativePhotograph =>
        _representativePhotographId is { } photographId
            ? _photographs.FirstOrDefault(photograph => photograph.Id == photographId)
            : null;

    private string CurrentPhotographCapturedOnValue =>
        CurrentPhotograph?.CapturedOnLocal ?? string.Empty;

    private string CurrentPhotographCapturedOnLabel =>
        string.IsNullOrEmpty(CurrentPhotographCapturedOnValue)
            ? Loc["Catch_PhotoCapturedOnUnknown"]
            : FormatLocalDateTime(CurrentPhotographCapturedOnValue);

    private string CurrentPhotographLocationLabel =>
        CurrentPhotograph?.Metadata.HasCoordinates == true
            ? Loc["Catch_PhotoLocationAvailable"]
            : Loc["Catch_PhotoLocationUnavailable"];

    private bool CanUseCurrentPhotographDetails =>
        CurrentPhotograph is { } photograph
        && (photograph.Metadata.CapturedOn.HasValue || photograph.Metadata.HasCoordinates);

    private static string FormatLocalDateTime(string localValue)
    {
        return DateTime.TryParseExact(
            localValue,
            ["yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed.ToString("g", CultureInfo.CurrentCulture)
            : localValue;
    }

    private IReadOnlyList<CatchPhotographCarouselItemModel> CarouselPhotographs =>
        _photographs
            .Select(photograph => new CatchPhotographCarouselItemModel(
                photograph.Id,
                photograph.ContentType,
                photograph.Bytes,
                null))
            .ToArray();

    private Task OnCameraPhotographSelected(InputFileChangeEventArgs args)
    {
        return AddSelectedPhotographsAsync(args, fromCamera: true);
    }

    private Task OnGalleryPhotographSelected(InputFileChangeEventArgs args)
    {
        return AddSelectedPhotographsAsync(args, fromCamera: false);
    }

    private async Task AddSelectedPhotographsAsync(InputFileChangeEventArgs args, bool fromCamera)
    {
        if (_isSaved)
        {
            return;
        }

        var rejectedUnsupported = false;
        var rejectedUnpreparable = false;
        foreach (var file in args.GetMultipleFiles(MaxSelectedPhotographs))
        {
            if (!PhotographContentTypeConstants.IsAllowed(file.ContentType))
            {
                rejectedUnsupported = true;
                continue;
            }

            if (!await AddPhotographAsync(file, fromCamera))
            {
                rejectedUnpreparable = true;
            }
        }

        _unsupportedFormat = rejectedUnsupported;
        _unpreparablePhotograph = rejectedUnpreparable;
        await ApplyPhotoMetadataAsync();
        TryStartOpportunisticCapture();
    }

    private async Task<bool> AddPhotographAsync(IBrowserFile file, bool fromCamera)
    {
        await using var stream = file.OpenReadStream(MaxPhotographBytes);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, _cancellationTokenSource.Token);
        var bytes = buffer.ToArray();
        var contentType = file.ContentType;
        var metadata = fromCamera
            ? PhotoMetadataModel.Empty
            : await ReadPhotoMetadataAsync(bytes, contentType, file.LastModified);
        var sanitised = await SanitisePhotographAsync(bytes, contentType);
        if (sanitised is null)
        {
            return false;
        }

        _fallbackCaughtOn ??= DateTimeOffset.UtcNow;
        var photograph = new PendingPhotograph(
            Guid.NewGuid(),
            contentType,
            sanitised,
            metadata,
            fromCamera,
            await ToLocalValueAsync(metadata.CapturedOn));
        _photographs.Add(photograph);
        _activePhotographId = photograph.Id;
        _saveFailed = false;
        return true;
    }

    private async Task<PhotoMetadataModel> ReadPhotoMetadataAsync(
        byte[] bytes,
        string contentType,
        DateTimeOffset fileLastModified)
    {
        try
        {
            return await PhotoMetadata.ReadAsync(
                bytes,
                contentType,
                fileLastModified,
                _cancellationTokenSource.Token);
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "reading photograph metadata",
                $"Photograph metadata could not be read ({exception.GetType().Name}).",
                CancellationToken.None);
            return PhotoMetadataModel.Empty;
        }
    }

    private async Task<byte[]?> SanitisePhotographAsync(byte[] bytes, string contentType)
    {
        try
        {
            return PhotoMetadata.Sanitise(bytes, contentType);
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "removing photograph metadata",
                $"Photograph metadata could not be removed ({exception.GetType().Name}).",
                CancellationToken.None);
            return null;
        }
    }

    private async Task<string?> ToLocalValueAsync(DateTimeOffset? instant)
    {
        return instant is null
            ? null
            : await Time.ToDateTimeLocalValueAsync(instant.Value, _cancellationTokenSource.Token);
    }

    private async Task ApplyPhotoMetadataAsync()
    {
        if (_photographs.Count == 0)
        {
            ResetPhotoMetadataState();
            ClearPhotoLocation();
            return;
        }

        var hasCameraPhotograph = _photographs.Any(photograph => photograph.FromCamera);
        if (hasCameraPhotograph || RepresentativePhotograph is null)
        {
            _representativePhotographId = null;
        }

        _proposal = hasCameraPhotograph
            ? PhotoMetadataProposalModel.Empty
            : PhotoMetadataProposal.Propose(
                [.. _photographs.Select(photograph => photograph.Metadata)],
                DateTimeOffset.UtcNow);
        await ApplyCaughtOnAsync();
        ApplyLocation();
    }

    private async Task ApplyCaughtOnAsync()
    {
        if (_caughtOnResolvedByAngler)
        {
            return;
        }

        var instant = ProposedCaughtOn() ?? _fallbackCaughtOn ?? DateTimeOffset.UtcNow;
        _proposedCaughtOn = instant;
        _caughtOnLocal = await Time.ToDateTimeLocalValueAsync(instant, _cancellationTokenSource.Token);
    }

    private DateTimeOffset? ProposedCaughtOn()
    {
        return _representativePhotographId is null
            ? _proposal.CaughtOn
            : RepresentativePhotograph?.Metadata.CapturedOn;
    }

    private void ApplyLocation()
    {
        if (_deviceLocationChosen && DeviceLocationApplied)
        {
            return;
        }

        if (RepresentativePhotograph is { } representative)
        {
            ApplyPhotographCoordinates(representative.Metadata);
            return;
        }

        if (CoordinateConflict)
        {
            _capturedLocation = null;
            return;
        }

        if (_proposal.HasCoordinates)
        {
            ApplyPhotoLocation(
                _proposal.Latitude!.Value,
                _proposal.Longitude!.Value,
                _proposal.CoordinatesCapturedOn ?? _proposal.CaughtOn);
            return;
        }

        ClearPhotoLocation();
    }

    private void ApplyPhotographCoordinates(PhotoMetadataModel metadata)
    {
        if (!metadata.HasCoordinates)
        {
            ClearPhotoLocation();
            return;
        }

        ApplyPhotoLocation(metadata.Latitude!.Value, metadata.Longitude!.Value, metadata.CapturedOn);
    }

    private void ApplyPhotoLocation(double latitude, double longitude, DateTimeOffset? capturedOn)
    {
        _capturedLocation = new CatchLocationModel(
            latitude,
            longitude,
            null,
            capturedOn ?? _fallbackCaughtOn ?? DateTimeOffset.UtcNow,
            LocationDefaults.PhotoMetadata,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
    }

    private void ClearPhotoLocation()
    {
        if (!PhotoLocationApplied)
        {
            return;
        }

        _capturedLocation = null;
    }

    private void ResetPhotoMetadataState()
    {
        _fallbackCaughtOn = null;
        _proposedCaughtOn = null;
        _caughtOnLocal = string.Empty;
        _caughtOnResolvedByAngler = false;
        _caughtOnInvalid = false;
        _representativePhotographId = null;
        _proposal = PhotoMetadataProposalModel.Empty;
    }

    private void OnCaughtOnChanged(string value)
    {
        _caughtOnLocal = value;
        _caughtOnResolvedByAngler = true;
        _caughtOnInvalid = false;
    }

    private async Task UseCurrentPhotographDetailsAsync()
    {
        if (CurrentPhotograph is not { } photograph || !CanUseCurrentPhotographDetails)
        {
            return;
        }

        _representativePhotographId = photograph.Id;
        if (photograph.Metadata.CapturedOn.HasValue)
        {
            _caughtOnResolvedByAngler = false;
        }

        if (photograph.Metadata.HasCoordinates)
        {
            _deviceLocationChosen = false;
        }

        await ApplyPhotoMetadataAsync();
    }

    private async Task RemovePhotographAsync(Guid photographId)
    {
        if (_isSaved)
        {
            return;
        }

        var removedIndex = _photographs.FindIndex(photograph => photograph.Id == photographId);
        if (removedIndex < 0)
        {
            return;
        }

        _photographs.RemoveAt(removedIndex);
        _activePhotographId = _photographs.Count == 0
            ? null
            : _photographs[Math.Min(removedIndex, _photographs.Count - 1)].Id;
        await ApplyPhotoMetadataAsync();
    }

    private void OnActivePhotographChanged(Guid? photographId)
    {
        _activePhotographId = photographId;
    }

    private async Task<DateTimeOffset?> TryResolveCaughtOnAsync()
    {
        var converted = await Time.FromDateTimeLocalValueAsync(
            _caughtOnLocal,
            _cancellationTokenSource.Token);
        if (converted is null)
        {
            return null;
        }

        var caughtOn = converted.Value.ToUniversalTime();
        if (_proposedCaughtOn is not null)
        {
            var proposedLocal = await Time.ToDateTimeLocalValueAsync(
                _proposedCaughtOn.Value,
                _cancellationTokenSource.Token);
            if (string.Equals(proposedLocal, _caughtOnLocal, StringComparison.Ordinal))
            {
                caughtOn = _proposedCaughtOn.Value.ToUniversalTime();
            }
        }

        return CatchDetailConstants.IsCaughtOnValid(caughtOn, DateTimeOffset.UtcNow)
            ? caughtOn
            : null;
    }

    private async Task SaveAsync()
    {
        if (!CanSave)
        {
            return;
        }

        _isSaving = true;
        _saveFailed = false;
        _caughtOnInvalid = false;
        await InvokeAsync(StateHasChanged);
        var saved = false;
        try
        {
            if (OwnerUserId == Guid.Empty)
            {
                throw new InvalidOperationException("The current user could not be resolved.");
            }

            var caughtOn = await TryResolveCaughtOnAsync();
            if (caughtOn is null)
            {
                _caughtOnInvalid = true;
                return;
            }

            var catchId = Guid.NewGuid();
            var photographs = _photographs
                .Select(photograph => new CatchPhotographModel(
                    photograph.Id,
                    catchId,
                    photograph.ContentType,
                    photograph.Bytes))
                .ToArray();
            var method = TrimToNull(_selectedMethod);
            var species = TrimToNull(_selectedSpecies);
            var location = _capturedLocation;
            await CatchStore.SaveAsync(
                new CatchModel(
                    catchId,
                    caughtOn.Value,
                    photographs,
                    species,
                    location,
                    OwnerUserId,
                    SyncStatus.SavedLocally,
                    SyncStatus.SavedLocally,
                    OwnerUserId,
                    OwnerUserId,
                    Method: method,
                    Weight: _weight,
                    Length: _length),
                _cancellationTokenSource.Token);
            _carriedMethod = method;
            _carriedSpecies = species;
            _carriedSpeciesWasExplicit = _speciesIsExplicit;
            _isSaved = true;
            _locationSaved = location is not null;
            saved = true;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("saving a catch locally", exception, CancellationToken.None);
            _saveFailed = true;
        }
        finally
        {
            _isSaving = false;
        }

        if (!saved)
        {
            return;
        }

        await Saved.InvokeAsync();
    }

    private void RecordAnotherCatch()
    {
        _photographs.Clear();
        ResetPhotoMetadataState();
        _weight = null;
        _length = null;
        _activePhotographId = null;
        _isSaved = false;
        _saveFailed = false;
        _unsupportedFormat = false;
        _unpreparablePhotograph = false;
        _capturedLocation = null;
        _deviceLocationChosen = false;
        _locationCaptureStarted = false;
        _locationSaved = false;
        _selectedMethod = _carriedMethod ?? string.Empty;
        _selectedSpecies = _carriedSpecies ?? string.Empty;
        _speciesIsExplicit = _carriedSpeciesWasExplicit;
    }

    private void SetWeight(decimal? value)
    {
        _weight = value;
    }

    private void SetLength(decimal? value)
    {
        _length = value;
    }

    private async Task AllowLocationAsync()
    {
        if (_photographs.Count > 0)
        {
            if (!_locationCaptureStarted)
            {
                _locationCaptureStarted = true;
                _ = CaptureLocationInBackgroundAsync(userRequested: true, replacePhotoLocation: false);
            }
        }
        else
        {
            try
            {
                await LocationService.TryCaptureAsync(true, _cancellationTokenSource.Token);
            }
            catch (Exception)
            {
            }
        }

        await RefreshLocationPromptAsync();
    }

    private async Task UseCurrentLocationAsync()
    {
        _locationCaptureStarted = true;
        await CaptureLocationInBackgroundAsync(userRequested: true, replacePhotoLocation: true);
        await RefreshLocationPromptAsync();
    }

    private async Task DismissLocationAsync()
    {
        try
        {
            await LocationService.DismissPromptAsync(_cancellationTokenSource.Token);
        }
        catch (Exception)
        {
        }

        await RefreshLocationPromptAsync();
    }

    private void TryStartOpportunisticCapture()
    {
        if (_isSaved
            || _locationCaptureStarted
            || _photographs.Count == 0
            || !_locationPrompt.WillCaptureOnSave
            || PhotoLocationApplied
            || CoordinateConflict)
        {
            return;
        }

        _locationCaptureStarted = true;
        _ = CaptureLocationInBackgroundAsync(userRequested: false, replacePhotoLocation: false);
    }

    private async Task CaptureLocationInBackgroundAsync(bool userRequested, bool replacePhotoLocation)
    {
        try
        {
            var location = await LocationService.TryCaptureAsync(
                userRequested,
                _cancellationTokenSource.Token);
            if (location is not null
                && CatchLocationConstants.AreCoordinatesValid(location.Latitude, location.Longitude)
                && (replacePhotoLocation
                    || (!PhotoLocationApplied && (userRequested || !CoordinateConflict))))
            {
                _capturedLocation = location;
                if (userRequested)
                {
                    _deviceLocationChosen = true;
                }
            }
        }
        catch (Exception)
        {
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task RefreshLocationPromptAsync()
    {
        try
        {
            _locationPrompt = await LocationService.GetPromptStatusAsync(_cancellationTokenSource.Token);
        }
        catch (Exception)
        {
            _locationPrompt = new LocationPromptStatus(false, true, false);
        }
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }

    private sealed record PendingPhotograph(
        Guid Id,
        string ContentType,
        byte[] Bytes,
        PhotoMetadataModel Metadata,
        bool FromCamera,
        string? CapturedOnLocal);
}
