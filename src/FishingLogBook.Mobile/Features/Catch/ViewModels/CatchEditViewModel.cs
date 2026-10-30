using System.ComponentModel;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using FishingLogBook.Mobile.Features.Measurements.Models;
using FishingLogBook.Mobile.Features.Measurements.ViewModels;
using FishingLogBook.Mobile.Features.Onboarding.Models;
using FishingLogBook.Mobile.Features.Onboarding.Services;
using FishingLogBook.Mobile.Features.Photographs.Mappers;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.Features.Photographs.ViewModels;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Catches.Models;
using FishingLogBook.Shared.Catches.Services;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Shared.Measurements;

namespace FishingLogBook.Mobile.Features.Catch.ViewModels;

public sealed class CatchEditViewModel : INotifyPropertyChanged, IDisposable
{
    private const int MaxChipOptions = 6;
    private static readonly TimeSpan DraftDebounce = TimeSpan.FromMilliseconds(1500);

    private readonly ICatchStore _catchStore;
    private readonly IAccountSessionCoordinator _sessionCoordinator;
    private readonly IAnglerPreferencesService _preferences;
    private readonly ICatchPhotographProposalService _proposalService;
    private readonly ICatchPhotographSaveArgsMapper _photographMapper;
    private readonly IPhotographFileStore _photographFileStore;
    private readonly IDeviceLocationService _locationService;
    private readonly IMeasurementService _measurement;
    private readonly IAccountUnitsService _units;
    private readonly TimeProvider _clock;
    private readonly UiTextService _text;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private CancellationTokenSource? _draftDebounce;
    private Guid _catchId;
    private bool _isEditingSavedCatch;
    private bool _draftRestored;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _isSaved;
    private bool _notSignedIn;
    private string? _statusKey;
    private string? _validationKey;
    private string _selectedMethod = string.Empty;
    private string _selectedSpecies = string.Empty;
    private bool _speciesIsExplicit;
    private string _baitOrLure = string.Empty;
    private string _notes = string.Empty;
    private DateTime _caughtOnLocal;
    private bool _caughtOnResolvedByAngler;
    private CatchPhotographProposalModel _proposal = CatchPhotographProposalModel.Empty;
    private LocalCatchLocationModel? _location;
    private bool _deviceLocationChosen;
    private decimal? _weightKg;
    private decimal? _lengthCm;
    private WeightUnitEnum _weightUnit = WeightUnitEnum.Kg;
    private LengthUnitEnum _lengthUnit = LengthUnitEnum.Cm;
    private AnglerPreferencesModel? _preferencesModel;
    private bool _disposed;

    public CatchEditViewModel(
        ICatchStore catchStore,
        IAccountSessionCoordinator sessionCoordinator,
        IAnglerPreferencesService preferences,
        ICatchPhotographProposalService proposalService,
        ICatchPhotographSaveArgsMapper photographMapper,
        IPhotographFileStore photographFileStore,
        IDeviceLocationService locationService,
        IMeasurementService measurement,
        IAccountUnitsService units,
        PhotographCollectionEditorViewModel photographs,
        TimeProvider clock,
        UiTextService text)
    {
        _catchStore = catchStore;
        _sessionCoordinator = sessionCoordinator;
        _preferences = preferences;
        _proposalService = proposalService;
        _photographMapper = photographMapper;
        _photographFileStore = photographFileStore;
        _locationService = locationService;
        _measurement = measurement;
        _units = units;
        Photographs = photographs;
        _clock = clock;
        _text = text;
        _caughtOnLocal = _clock.GetUtcNow().UtcDateTime;
        Photographs.ItemsChanged += OnPhotographsChanged;
        _text.PropertyChanged += OnCultureChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UiTextService Text => _text;

    public PhotographCollectionEditorViewModel Photographs { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set => Set(ref _isLoading, value, nameof(IsLoading), nameof(CanInteract));
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set => Set(ref _isSaving, value, nameof(IsSaving), nameof(CanSave), nameof(CanInteract));
    }

    public bool IsSaved
    {
        get => _isSaved;
        private set => Set(ref _isSaved, value, nameof(IsSaved), nameof(CanSave), nameof(CanInteract));
    }

    public bool NotSignedIn
    {
        get => _notSignedIn;
        private set => Set(ref _notSignedIn, value, nameof(NotSignedIn), nameof(CanInteract));
    }

    public bool DraftRestored
    {
        get => _draftRestored;
        private set => Set(ref _draftRestored, value, nameof(DraftRestored), nameof(ShowDraftRestored));
    }

    public bool ShowDraftRestored => DraftRestored && !IsEditingSavedCatch;

    public bool IsEditingSavedCatch
    {
        get => _isEditingSavedCatch;
        private set => Set(ref _isEditingSavedCatch, value, nameof(IsEditingSavedCatch), nameof(PageTitle));
    }

    public string PageTitle => _text[IsEditingSavedCatch ? "EditCatch" : "RecordCatch"];

    public bool CanInteract => !IsLoading && !NotSignedIn && !IsSaved;

    public bool CanSave =>
        CanInteract
        && !IsSaving
        && Photographs.Items.Count > 0
        && !DateConflict
        && !HasValidation;

    public bool HasStatus => _statusKey is not null;

    public string StatusText => _statusKey is null ? string.Empty : _text[_statusKey];

    public bool HasValidation => _validationKey is not null;

    public string ValidationText => _validationKey is null ? string.Empty : _text[_validationKey];

    public string ValidationKey => _validationKey ?? string.Empty;

    public bool DateConflict =>
        _proposal.HasConflictingDates && !_caughtOnResolvedByAngler;

    public bool ShowDateConflict => DateConflict && CanInteract;

    public bool ShowPhotoConflict =>
        (_proposal.HasConflictingDates || _proposal.HasConflictingCoordinates) && CanInteract;

    public string PhotoConflictText => _text[_proposal.HasConflictingDates && _proposal.HasConflictingCoordinates
        ? "CatchPhotoDateAndLocationConflict"
        : _proposal.HasConflictingDates
            ? "CatchPhotoDateConflict"
            : "CatchPhotoLocationConflict"];

    public DateTime CaughtOnLocal
    {
        get => _caughtOnLocal;
        set
        {
            var combined = value.Date + _caughtOnLocal.TimeOfDay;
            if (_caughtOnLocal == combined)
            {
                return;
            }

            _caughtOnLocal = combined;
            _caughtOnResolvedByAngler = true;
            NotifyCaughtOn();
            ScheduleDraftSave();
        }
    }

    public TimeSpan CaughtOnTime
    {
        get => _caughtOnLocal.TimeOfDay;
        set
        {
            var combined = _caughtOnLocal.Date + value;
            if (_caughtOnLocal == combined)
            {
                return;
            }

            _caughtOnLocal = combined;
            _caughtOnResolvedByAngler = true;
            NotifyCaughtOn();
            ScheduleDraftSave();
        }
    }

    public string BaitOrLure
    {
        get => _baitOrLure;
        set
        {
            if (_baitOrLure == value)
            {
                return;
            }

            _baitOrLure = value ?? string.Empty;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BaitOrLure)));
            ScheduleDraftSave();
        }
    }

    public string Notes
    {
        get => _notes;
        set
        {
            if (_notes == value)
            {
                return;
            }

            _notes = value ?? string.Empty;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Notes)));
            ScheduleDraftSave();
        }
    }

    public string LocationCaption =>
        _location is null
            ? _text["CatchLocationNotRecorded"]
            : string.Format(_text.Culture, _text["CatchLocationPrivateCaption"], _location.Latitude, _location.Longitude);

    public bool HasLocation => _location is not null;

    public string WeightSummary => FormatMeasurement(_weightKg, true);

    public string LengthSummary => FormatMeasurement(_lengthCm, false);

    public IReadOnlyList<CatchChipOptionModel> MethodOptions => BuildMethodOptions();

    public IReadOnlyList<CatchChipOptionModel> SpeciesOptions => BuildSpeciesOptions();

    public MeasurementEditorViewModel CreateWeightEditor()
    {
        return new MeasurementEditorViewModel(_measurement, _text, MeasurementKindEnum.Weight, _weightKg, _weightUnit, _lengthUnit);
    }

    public MeasurementEditorViewModel CreateLengthEditor()
    {
        return new MeasurementEditorViewModel(_measurement, _text, MeasurementKindEnum.Length, _lengthCm, _weightUnit, _lengthUnit);
    }

    public void ApplyWeightEditor(MeasurementEditorViewModel editor)
    {
        _weightKg = editor.CanonicalValue;
        NotifyMeasurements();
        ScheduleDraftSave();
    }

    public void ApplyLengthEditor(MeasurementEditorViewModel editor)
    {
        _lengthCm = editor.CanonicalValue;
        NotifyMeasurements();
        ScheduleDraftSave();
    }

    public bool CanRemovePhoto => !IsEditingSavedCatch || Photographs.Items.Count > 1;

    public async Task InitializeAsync(Guid? existingCatchId, CancellationToken cancellationToken)
    {
        IsLoading = true;
        _validationKey = null;
        _statusKey = null;
        try
        {
            var lease = _sessionCoordinator.Capture();
            if (lease is null)
            {
                NotSignedIn = true;
                return;
            }

            NotSignedIn = false;
            Photographs.UserId = lease.UserId;
            var units = await _units.GetAsync(cancellationToken);
            _weightUnit = units.WeightUnit;
            _lengthUnit = units.LengthUnit;
            _preferencesModel = await _preferences.GetCachedAsync(cancellationToken) ?? AnglerPreferencesModel.Empty;

            if (existingCatchId is { } catchId && catchId != Guid.Empty)
            {
                await LoadExistingAsync(catchId, cancellationToken);
                return;
            }

            await InitializeNewAsync(lease.UserId, cancellationToken);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void SelectMethod(string method)
    {
        if (!CanInteract || string.Equals(_selectedMethod, method, StringComparison.Ordinal))
        {
            return;
        }

        _selectedMethod = method;
        ApplyDefaultSpeciesForMethod();
        NotifyChipState();
        ScheduleDraftSave();
    }

    public void SelectSpecies(string species)
    {
        if (!CanInteract)
        {
            return;
        }

        _selectedSpecies = species;
        _speciesIsExplicit = true;
        NotifyChipState();
        ScheduleDraftSave();
    }

    public bool IsMethodSelected(string name) =>
        string.Equals(_selectedMethod, name, StringComparison.OrdinalIgnoreCase);

    public bool IsSpeciesSelected(string name) =>
        string.Equals(_selectedSpecies, name, StringComparison.OrdinalIgnoreCase);

    public async Task CaptureLocationAsync(CancellationToken cancellationToken)
    {
        if (!CanInteract)
        {
            return;
        }

        var capture = await _locationService.CaptureCurrentAsync(cancellationToken);
        if (capture.Outcome != Onboarding.Enums.LocationCaptureOutcomeEnum.Captured
            || capture.Latitude is null
            || capture.Longitude is null
            || capture.CapturedOn is null)
        {
            _statusKey = "CatchLocationCaptureFailed";
            NotifyStatus();
            return;
        }

        _deviceLocationChosen = true;
        _location = new LocalCatchLocationModel
        {
            Latitude = capture.Latitude.Value,
            Longitude = capture.Longitude.Value,
            AccuracyMetres = capture.AccuracyMetres,
            CapturedOn = capture.CapturedOn.Value,
            Source = LocationDefaults.DeviceGps,
            Visibility = LocationDefaults.Private,
            ConsentVersion = LocationDefaults.ConsentVersion
        };
        _statusKey = null;
        NotifyLocation();
        ScheduleDraftSave();
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        _draftDebounce?.Cancel();
        if (!CanSave)
        {
            _validationKey = ResolveValidationKey();
            NotifyValidation();
            return false;
        }

        return await PersistAsync(LocalCatchLifecycleEnum.Saved, cancellationToken);
    }

    public async Task OnDisappearingAsync(CancellationToken cancellationToken)
    {
        _draftDebounce?.Cancel();
        if (!CanInteract || IsSaved)
        {
            return;
        }

        if (IsEditingSavedCatch)
        {
            // Flush valid edits as Saved so navigate-away / modal opens never demote lifecycle.
            if (ResolveValidationKey() is null)
            {
                await PersistAsync(LocalCatchLifecycleEnum.Saved, cancellationToken, markUiSaved: false);
            }

            return;
        }

        await SaveDraftAsync(cancellationToken);
    }

    public void RemovePhoto(Guid photographId)
    {
        if (!CanRemovePhoto)
        {
            _validationKey = "CatchCannotRemoveLastPhoto";
            NotifyValidation();
            return;
        }

        Photographs.Remove(photographId);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Photographs.ItemsChanged -= OnPhotographsChanged;
        _text.PropertyChanged -= OnCultureChanged;
        _draftDebounce?.Cancel();
        _draftDebounce?.Dispose();
        _saveGate.Dispose();
    }

    private async Task InitializeNewAsync(Guid userId, CancellationToken cancellationToken)
    {
        IsEditingSavedCatch = false;
        Photographs.MinimumCount = 0;
        _catchId = Guid.NewGuid();
        ApplyProfileDefaults();
        var draft = (await _catchStore.ListAsync(cancellationToken))
            .Where(record => record.Lifecycle == LocalCatchLifecycleEnum.Draft)
            .OrderByDescending(record => record.UpdatedOn)
            .FirstOrDefault();
        if (draft is not null)
        {
            await BindRecordAsync(draft, cancellationToken);
            DraftRestored = true;
            return;
        }

        DraftRestored = false;
        await ApplyPhotoMetadataAsync();
    }

    private async Task LoadExistingAsync(Guid catchId, CancellationToken cancellationToken)
    {
        var record = await _catchStore.GetAsync(catchId, cancellationToken);
        if (record is null)
        {
            _validationKey = "CatchEditNotFound";
            NotifyValidation();
            return;
        }

        IsEditingSavedCatch = record.Lifecycle == LocalCatchLifecycleEnum.Saved;
        Photographs.MinimumCount = IsEditingSavedCatch ? 1 : 0;
        _catchId = record.Id;
        await BindRecordAsync(record, cancellationToken);
    }

    private async Task BindRecordAsync(LocalCatchRecordModel record, CancellationToken cancellationToken)
    {
        _catchId = record.Id;
        _selectedMethod = record.Method ?? string.Empty;
        _selectedSpecies = record.SpeciesName ?? string.Empty;
        _speciesIsExplicit = !string.IsNullOrWhiteSpace(record.SpeciesName);
        _baitOrLure = record.BaitOrLure ?? string.Empty;
        _notes = record.Notes ?? string.Empty;
        _weightKg = record.Weight is null ? null : (decimal)record.Weight;
        _lengthCm = record.Length is null ? null : (decimal)record.Length;
        _location = record.Location;
        _deviceLocationChosen = record.Location?.Source == LocationDefaults.DeviceGps;
        _caughtOnLocal = record.CaughtOn.UtcDateTime;
        _caughtOnResolvedByAngler = true;
        var items = record.Photographs
            .OrderBy(photograph => photograph.SortOrder)
            .Select(photograph =>
            {
                var relative = photograph.LocalFilePath ?? string.Empty;
                var preview = relative.Length == 0
                    ? string.Empty
                    : _photographFileStore.GetAbsolutePath(record.UserId, relative);
                return new PhotographEditorItemModel
                {
                    Id = photograph.Id,
                    SortOrder = photograph.SortOrder,
                    PreviewPath = preview,
                    RelativeFilePath = relative,
                    ContentType = photograph.ContentType ?? "image/jpeg"
                };
            })
            .ToArray();
        Photographs.SetItems(items);
        NotifyChipState();
        NotifyMeasurements();
        NotifyLocation();
        NotifyCaughtOn();
        await ApplyPhotoMetadataAsync();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void ApplyProfileDefaults()
    {
        if (_preferencesModel is null)
        {
            return;
        }

        var preferredMethod = _preferencesModel.Preferences.Methods.FirstOrDefault(method => method.IsDefault);
        if (preferredMethod is not null)
        {
            _selectedMethod = preferredMethod.Name;
        }
        else if (_preferencesModel.Catalogue.Methods.FirstOrDefault() is { } catalogueMethod)
        {
            _selectedMethod = catalogueMethod.Name;
        }

        if (!string.IsNullOrWhiteSpace(_selectedMethod))
        {
            ApplyDefaultSpeciesForMethod();
        }

        NotifyChipState();
    }

    private void ApplyDefaultSpeciesForMethod()
    {
        if (_speciesIsExplicit)
        {
            return;
        }

        var method = FindMethodPreference(_selectedMethod);
        var defaultSpecies = method?.Species.FirstOrDefault(species => species.IsDefault)
            ?? method?.Species.FirstOrDefault();
        _selectedSpecies = defaultSpecies?.Name ?? string.Empty;
    }

    private FishingMethodPreferenceDto? FindMethodPreference(string methodName)
    {
        if (_preferencesModel is null || string.IsNullOrWhiteSpace(methodName))
        {
            return null;
        }

        return _preferencesModel.Preferences.Methods
            .FirstOrDefault(method => string.Equals(method.Name, methodName, StringComparison.OrdinalIgnoreCase))
            ?? _preferencesModel.Preferences.Methods
                .FirstOrDefault(method => string.Equals(method.Code, methodName, StringComparison.OrdinalIgnoreCase));
    }

    private IReadOnlyList<CatchChipOptionModel> BuildMethodOptions()
    {
        if (_preferencesModel is null)
        {
            return [];
        }

        var preferred = _preferencesModel.Preferences.Methods
            .OrderByDescending(method => method.IsDefault)
            .Select(method => new CatchChipOptionModel(method.Code, method.Name))
            .ToArray();
        var options = preferred.Length > 0
            ? preferred
            : _preferencesModel.Catalogue.Methods
                .Select(method => new CatchChipOptionModel(method.Code, method.Name))
                .ToArray();
        return CatchChipOptionModel.BuildShortlist(options, _selectedMethod, MaxChipOptions);
    }

    private IReadOnlyList<CatchChipOptionModel> BuildSpeciesOptions()
    {
        if (_preferencesModel is null)
        {
            return [];
        }

        var preferred = FindMethodPreference(_selectedMethod)?.Species
            .OrderByDescending(species => species.IsDefault)
            .Select(species => new CatchChipOptionModel(species.Code, species.Name))
            .ToArray() ?? [];
        return CatchChipOptionModel.BuildShortlist(preferred, _selectedSpecies, MaxChipOptions);
    }

    private async void OnPhotographsChanged(object? sender, EventArgs args)
    {
        await ApplyPhotoMetadataAsync();
        ScheduleDraftSave();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSave)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRemovePhoto)));
    }

    private async Task ApplyPhotoMetadataAsync()
    {
        if (Photographs.Items.Count == 0)
        {
            _proposal = CatchPhotographProposalModel.Empty;
            NotifyConflictState();
            return;
        }

        _proposal = _proposalService.Propose(
            Photographs.Items.Select(item => item.Metadata).ToArray(),
            _clock.GetUtcNow());
        if (!_caughtOnResolvedByAngler && _proposal.CaughtOn is { } proposed)
        {
            _caughtOnLocal = proposed.UtcDateTime;
            NotifyCaughtOn();
        }

        ApplyProposalLocation();
        NotifyConflictState();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSave)));
        await Task.CompletedTask;
    }

    private void ApplyProposalLocation()
    {
        if (_deviceLocationChosen && _location?.Source == LocationDefaults.DeviceGps)
        {
            return;
        }

        if (_proposal.HasConflictingCoordinates)
        {
            if (_location?.Source == LocationDefaults.PhotoMetadata)
            {
                _location = null;
                NotifyLocation();
            }

            return;
        }

        if (!_proposal.HasCoordinates)
        {
            return;
        }

        _location = new LocalCatchLocationModel
        {
            Latitude = _proposal.Latitude!.Value,
            Longitude = _proposal.Longitude!.Value,
            AccuracyMetres = null,
            CapturedOn = _proposal.CoordinatesCapturedOn ?? _proposal.CaughtOn ?? _clock.GetUtcNow(),
            Source = LocationDefaults.PhotoMetadata,
            Visibility = LocationDefaults.Private,
            ConsentVersion = LocationDefaults.ConsentVersion
        };
        NotifyLocation();
    }

    private void ScheduleDraftSave()
    {
        // Saved-catch edits must not be written as Draft (would demote the journal row).
        if (!CanInteract || IsSaved || IsEditingSavedCatch)
        {
            return;
        }

        _draftDebounce?.Cancel();
        _draftDebounce = new CancellationTokenSource();
        var token = _draftDebounce.Token;
        _ = DebouncedDraftSaveAsync(token);
    }

    private async Task DebouncedDraftSaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(DraftDebounce, cancellationToken);
            await SaveDraftAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private Task SaveDraftAsync(CancellationToken cancellationToken)
    {
        return PersistAsync(LocalCatchLifecycleEnum.Draft, cancellationToken);
    }

    private async Task<bool> PersistAsync(
        LocalCatchLifecycleEnum lifecycle,
        CancellationToken cancellationToken,
        bool markUiSaved = true)
    {
        if (lifecycle == LocalCatchLifecycleEnum.Saved)
        {
            _validationKey = ResolveValidationKey();
            if (_validationKey is not null)
            {
                NotifyValidation();
                return false;
            }
        }

        if (lifecycle == LocalCatchLifecycleEnum.Draft)
        {
            if (!await _saveGate.WaitAsync(0, cancellationToken))
            {
                return false;
            }
        }
        else
        {
            await _saveGate.WaitAsync(cancellationToken);
        }

        try
        {
            if (lifecycle == LocalCatchLifecycleEnum.Saved && markUiSaved)
            {
                IsSaving = true;
            }

            var lease = _sessionCoordinator.Capture();
            if (lease is null)
            {
                NotSignedIn = true;
                return false;
            }

            var caughtOn = new DateTimeOffset(DateTime.SpecifyKind(_caughtOnLocal, DateTimeKind.Utc));
            if (lifecycle == LocalCatchLifecycleEnum.Saved
                && !CatchDetailConstants.IsCaughtOnValid(caughtOn, _clock.GetUtcNow()))
            {
                _validationKey = "CatchCaughtOnInvalid";
                NotifyValidation();
                return false;
            }

            var args = new LocalCatchSaveArgs
            {
                Id = _catchId,
                CaughtOn = caughtOn,
                SpeciesName = TrimToNull(_selectedSpecies),
                Weight = _weightKg is null ? null : (double)_weightKg.Value,
                Length = _lengthCm is null ? null : (double)_lengthCm.Value,
                Method = TrimToNull(_selectedMethod),
                BaitOrLure = TrimToNull(_baitOrLure),
                Notes = TrimToNull(_notes),
                PlaceName = null,
                Location = _location,
                Lifecycle = lifecycle,
                CaughtByUserId = lease.UserId,
                RecordedByUserId = lease.UserId,
                Photographs = _photographMapper.Map(Photographs.Items.ToArray())
            };

            var result = await _catchStore.SaveAsync(args, cancellationToken);
            if (!result.IsSuccess)
            {
                _statusKey = result.Outcome switch
                {
                    LocalCatchSaveOutcomeEnum.NotSignedIn => "CatchSaveNotSignedIn",
                    _ => "CatchSaveFailed"
                };
                NotifyStatus();
                return false;
            }

            if (lifecycle == LocalCatchLifecycleEnum.Saved && markUiSaved)
            {
                IsSaved = true;
                _statusKey = "CatchSavedLocally";
                NotifyStatus();
            }

            return true;
        }
        finally
        {
            IsSaving = false;
            _saveGate.Release();
        }
    }

    private string? ResolveValidationKey()
    {
        if (Photographs.Items.Count == 0)
        {
            return "CatchSaveRequiresPhoto";
        }

        if (DateConflict)
        {
            return "CatchPhotoDateConflict";
        }

        if (!CatchDetailConstants.IsWeightValid(_weightKg))
        {
            return "CatchWeightInvalid";
        }

        if (!CatchDetailConstants.IsLengthValid(_lengthCm))
        {
            return "CatchLengthInvalid";
        }

        if (!CatchDetailConstants.IsOptionalTextValid(_selectedSpecies, CatchDetailConstants.MaxSpeciesNameLength)
            || !CatchDetailConstants.IsOptionalTextValid(_selectedMethod, CatchDetailConstants.MaxMethodLength)
            || !CatchDetailConstants.IsOptionalTextValid(_baitOrLure, CatchDetailConstants.MaxBaitOrLureLength)
            || !CatchDetailConstants.IsOptionalTextValid(_notes, CatchDetailConstants.MaxNotesLength))
        {
            return "CatchTextTooLong";
        }

        return null;
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private string FormatMeasurement(decimal? canonical, bool weight)
    {
        if (canonical is null)
        {
            return _text["NotRecorded"];
        }

        if (weight)
        {
            var display = _measurement.ToDisplayWeight(canonical, _weightUnit);
            var unit = _text[_weightUnit == WeightUnitEnum.Lb ? "MeasureUnitLb" : "MeasureUnitKg"];
            return display is null ? _text["NotRecorded"] : $"{display.Value.ToString("0.##", _text.Culture)} {unit}";
        }

        var length = _measurement.ToDisplayLength(canonical, _lengthUnit);
        var lengthUnit = _text[_lengthUnit == LengthUnitEnum.In ? "MeasureUnitIn" : "MeasureUnitCm"];
        return length is null ? _text["NotRecorded"] : $"{length.Value.ToString("0.##", _text.Culture)} {lengthUnit}";
    }

    private void OnCultureChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(UiTextService.Culture))
        {
            return;
        }

        NotifyChipState();
        NotifyMeasurements();
        NotifyLocation();
        NotifyStatus();
        NotifyValidation();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PageTitle)));
    }

    private void NotifyChipState()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MethodOptions)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SpeciesOptions)));
    }

    private void NotifyCaughtOn()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CaughtOnLocal)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CaughtOnTime)));
        NotifyConflictState();
    }

    private void NotifyConflictState()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DateConflict)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowDateConflict)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowPhotoConflict)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PhotoConflictText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSave)));
    }

    private void NotifyLocation()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasLocation)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocationCaption)));
    }

    private void NotifyMeasurements()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WeightSummary)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LengthSummary)));
    }

    private void NotifyStatus()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasStatus)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
    }

    private void NotifyValidation()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasValidation)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValidationText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValidationKey)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSave)));
    }

    private void Set<T>(ref T field, T value, params string[] names)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        foreach (var name in names)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
