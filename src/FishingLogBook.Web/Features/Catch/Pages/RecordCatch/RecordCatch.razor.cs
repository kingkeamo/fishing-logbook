using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Pages.RecordCatch;

public partial class RecordCatch : ComponentBase, IDisposable
{
    private const long MaxPhotographBytes = 10 * 1024 * 1024;
    private const double SwipeThresholdPixels = 40;
    private const int MaxChipOptions = 6;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<PendingPhotograph> _photographs = [];
    private DateTimeOffset? _caughtOn;
    private int _carouselIndex;
    private double _pointerStartX;
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
            await LoadCatalogueAsync();
            ApplyProfileDefaults();
            await RefreshLocationPromptAsync();
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
        return result?.Option;
    }

    private bool CanSave
    {
        get
        {
            return !_isSaved && _photographs.Count > 0 && !_isSaving;
        }
    }

    private bool CanShowPrevious
    {
        get
        {
            return _carouselIndex > 0;
        }
    }

    private bool CanShowNext
    {
        get
        {
            return _carouselIndex < _photographs.Count - 1;
        }
    }

    private string CaughtOnDisplay
    {
        get
        {
            return _caughtOn?.ToString("g") ?? string.Empty;
        }
    }

    private string PhotoPosition
    {
        get
        {
            return Loc["Catch_PhotoPosition", _carouselIndex + 1, _photographs.Count];
        }
    }

    private PendingPhotograph? CurrentPhotograph
    {
        get
        {
            if (_photographs.Count == 0
                || _carouselIndex < 0
                || _carouselIndex >= _photographs.Count)
            {
                return null;
            }

            return _photographs[_carouselIndex];
        }
    }

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
        _photographs.Add(new PendingPhotograph(
            Guid.NewGuid(),
            contentType,
            bytes,
            $"data:{contentType};base64,{Convert.ToBase64String(bytes)}"));
        _carouselIndex = _photographs.Count - 1;
        _saveFailed = false;
    }

    private void RemoveCurrentPhotograph()
    {
        var current = CurrentPhotograph;
        if (_isSaved || current is null)
        {
            return;
        }

        var removedIndex = _photographs.FindIndex(photograph => photograph.Id == current.Id);
        _photographs.RemoveAll(photograph => photograph.Id == current.Id);
        if (_photographs.Count == 0)
        {
            _carouselIndex = 0;
            _caughtOn = null;
            return;
        }

        _carouselIndex = Math.Min(removedIndex, _photographs.Count - 1);
    }

    private void ShowPrevious()
    {
        if (!CanShowPrevious)
        {
            return;
        }

        _carouselIndex -= 1;
    }

    private void ShowNext()
    {
        if (!CanShowNext)
        {
            return;
        }

        _carouselIndex += 1;
    }

    private void OnPointerDown(PointerEventArgs args)
    {
        _pointerStartX = args.ClientX;
    }

    private void OnPointerUp(PointerEventArgs args)
    {
        var delta = args.ClientX - _pointerStartX;
        if (delta <= -SwipeThresholdPixels)
        {
            ShowNext();
            return;
        }

        if (delta >= SwipeThresholdPixels)
        {
            ShowPrevious();
        }
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
                "production catch synchronisation",
                exception,
                CancellationToken.None);
        }
    }

    private void RecordAnotherCatch()
    {
        _photographs.Clear();
        _caughtOn = null;
        _carouselIndex = 0;
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

    private sealed record PendingPhotograph(
        Guid Id,
        string ContentType,
        byte[] Bytes,
        string PreviewUrl);
}
