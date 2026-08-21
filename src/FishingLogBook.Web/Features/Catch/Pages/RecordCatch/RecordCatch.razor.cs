using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
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

namespace FishingLogBook.Web.Features.Catch.Pages.RecordCatch;

public partial class RecordCatch : ComponentBase, IDisposable
{
    private const long MaxPhotographBytes = 10 * 1024 * 1024;
    private const int MaxChipOptions = 6;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<PendingPhotograph> _photographs = [];
    private DateTimeOffset? _caughtOn;
    private Guid? _activePhotographId;
    private string _selectedMethod = string.Empty;
    private string _selectedSpecies = string.Empty;
    private bool _methodIsExplicit;
    private bool _speciesIsExplicit;
    private string? _carriedMethod;
    private string? _carriedSpecies;
    private bool _carriedSpeciesWasExplicit;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _isSaved;
    private bool _saveFailed;
    private bool _unsupportedFormat;
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
    private ILocalCatchOwnerService LocalCatchOwner { get; set; } = default!;

    [Inject]
    private ICatchSynchroniser CatchSynchroniser { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private ILocationService LocationService { get; set; } = default!;

    [Inject]
    private IAnglerPreferencesProvider AnglerPreferences { get; set; } = default!;

    [Inject]
    private IModalService ModalService { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var catalogue = LoadCatalogueAsync();
            var locationPrompt = RefreshLocationPromptAsync();
            await Task.WhenAll(catalogue, locationPrompt);
            ApplyProfileDefaults();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadCatalogueAsync()
    {
        var anglerPreferences = await AnglerPreferences.GetAsync(_cancellationTokenSource.Token);
        _catalogueMethods = anglerPreferences.Catalogue.Methods;
        _catalogueSpecies = anglerPreferences.Catalogue.AllSpecies;
        _preferences = anglerPreferences.Preferences;
        _catalogueUnavailable = !anglerPreferences.HasCatalogue;
    }

    private void ApplyProfileDefaults()
    {
        var defaultMethod = _preferences?.Methods.FirstOrDefault(method => method.IsDefault);
        if (defaultMethod is null)
        {
            return;
        }

        _selectedMethod = defaultMethod.Name;
        _methodIsExplicit = false;
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
        _methodIsExplicit = !string.IsNullOrWhiteSpace(method);
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
            return !_isSaved && _photographs.Count > 0 && !_isSaving;
        }
    }

    private string CaughtOnDisplay
    {
        get
        {
            return _caughtOn?.ToString("g") ?? string.Empty;
        }
    }

    private IReadOnlyList<CatchPhotographCarouselItemModel> CarouselPhotographs =>
        _photographs
            .Select(photograph => new CatchPhotographCarouselItemModel(
                photograph.Id,
                photograph.ContentType,
                photograph.Bytes,
                null))
            .ToArray();

    private async Task OnPhotographSelected(InputFileChangeEventArgs args)
    {
        if (_isSaved)
        {
            return;
        }

        var rejectedUnsupported = false;
        foreach (var file in args.GetMultipleFiles(10))
        {
            if (!PhotographContentTypeConstants.IsAllowed(file.ContentType))
            {
                rejectedUnsupported = true;
                continue;
            }

            await AddPhotographAsync(file);
        }

        _unsupportedFormat = rejectedUnsupported;
        TryStartOpportunisticCapture();
    }

    private async Task AddPhotographAsync(IBrowserFile file)
    {
        await using var stream = file.OpenReadStream(MaxPhotographBytes);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, _cancellationTokenSource.Token);
        var bytes = buffer.ToArray();
        var contentType = file.ContentType;
        _caughtOn ??= DateTimeOffset.UtcNow;
        var photograph = new PendingPhotograph(
            Guid.NewGuid(),
            contentType,
            bytes);
        _photographs.Add(photograph);
        _activePhotographId = photograph.Id;
        _saveFailed = false;
    }

    private void RemovePhotograph(Guid photographId)
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
        if (_photographs.Count == 0)
        {
            _activePhotographId = null;
            _caughtOn = null;
            return;
        }

        _activePhotographId = _photographs[Math.Min(removedIndex, _photographs.Count - 1)].Id;
    }

    private void OnActivePhotographChanged(Guid? photographId)
    {
        _activePhotographId = photographId;
    }

    private async Task SaveAsync()
    {
        if (!CanSave)
        {
            return;
        }

        _isSaving = true;
        _saveFailed = false;
        await InvokeAsync(StateHasChanged);
        var saved = false;
        try
        {
            var ownerUserId = await LocalCatchOwner.GetUserIdAsync(_cancellationTokenSource.Token);
            if (ownerUserId == Guid.Empty)
            {
                throw new InvalidOperationException("The current user could not be resolved.");
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
                    _caughtOn ?? DateTimeOffset.UtcNow,
                    photographs,
                    species,
                    location,
                    ownerUserId,
                    SyncStatus.SavedLocally,
                    SyncStatus.SavedLocally,
                    ownerUserId,
                    ownerUserId,
                    Method: method),
                _cancellationTokenSource.Token);
            _carriedMethod = method;
            _carriedSpecies = species;
            _carriedSpeciesWasExplicit = _speciesIsExplicit;
            _isSaved = true;
            _locationSaved = location is not null;
            saved = true;
        }
        catch (Exception)
        {
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

        TryToSynchronisePending();
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

    private void RecordAnotherCatch()
    {
        _photographs.Clear();
        _caughtOn = null;
        _activePhotographId = null;
        _isSaved = false;
        _saveFailed = false;
        _unsupportedFormat = false;
        _capturedLocation = null;
        _locationCaptureStarted = false;
        _locationSaved = false;
        _selectedMethod = _carriedMethod ?? string.Empty;
        _methodIsExplicit = _carriedMethod is not null;
        _selectedSpecies = _carriedSpecies ?? string.Empty;
        _speciesIsExplicit = _carriedSpeciesWasExplicit;
    }

    private async Task AllowLocationAsync()
    {
        if (_photographs.Count > 0)
        {
            if (!_locationCaptureStarted)
            {
                _locationCaptureStarted = true;
                _ = CaptureLocationInBackgroundAsync(userRequested: true);
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
        if (_isSaved || _locationCaptureStarted || _photographs.Count == 0 || !_locationPrompt.WillCaptureOnSave)
        {
            return;
        }

        _locationCaptureStarted = true;
        _ = CaptureLocationInBackgroundAsync(userRequested: false);
    }

    private async Task CaptureLocationInBackgroundAsync(bool userRequested)
    {
        try
        {
            var location = await LocationService.TryCaptureAsync(
                userRequested,
                _cancellationTokenSource.Token);
            if (location is not null
                && CatchLocationConstants.AreCoordinatesValid(location.Latitude, location.Longitude))
            {
                _capturedLocation = location;
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

    private sealed record PendingPhotograph(Guid Id, string ContentType, byte[] Bytes);
}
