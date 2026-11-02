using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Import.Services;
using FishingLogBook.Mobile.Features.Measurements.Models;
using FishingLogBook.Mobile.Features.Measurements.ViewModels;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Profile.ViewModels;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Shared.Measurements;
using FishingLogBook.Shared.Photographs.Contracts;

namespace FishingLogBook.Mobile.Features.Import.ViewModels;

public sealed class ImportViewModel : INotifyPropertyChanged
{
    public const int MaxChipOptions = 6;
    public const string MoreOptionCode = "__more__";

    private readonly IAnglerPreferencesService _preferences;
    private readonly IImportCatchProposalService _catchProposals;
    private readonly IImportTripProposalService _tripProposals;
    private readonly IImportExistingTripService _existingTrips;
    private readonly IImportLocationLookupService _locations;
    private readonly IImportPhotoPreparationService _preparation;
    private readonly IImportPersistenceService _persistence;
    private readonly IPhotographMediaAdapter _media;
    private readonly IPhotographLocalTimeMapper _localTime;
    private readonly IConnectivityService _connectivity;
    private readonly IImportModalService _modals;
    private readonly IMeasurementService _measurement;
    private readonly IAccountUnitsService _units;
    private AnglerPreferencesModel _preferencesModel = AnglerPreferencesModel.Empty;
    private WeightUnitEnum _weightUnit = WeightUnitEnum.Kg;
    private LengthUnitEnum _lengthUnit = LengthUnitEnum.Cm;
    private ImportBatchModel _batch = new(Guid.NewGuid(), EmptySelection(), EmptySelection());
    private Guid _methodId;
    private Guid _speciesId;
    private bool _isLoading = true;
    private bool _isBusy;
    private bool _selectionLimitExceeded;
    private bool _isPersisting;
    private bool _isPersisted;
    private string? _message;
    private ImportPersistenceProgressModel? _progress;
    private IReadOnlyDictionary<Guid, IReadOnlyList<TripSummaryDto>> _existingTripLookup =
        new Dictionary<Guid, IReadOnlyList<TripSummaryDto>>();
    private readonly Dictionary<Guid, HashSet<Guid>> _selectedPhotos = [];
    private CancellationTokenSource? _persistenceCancellation;
    private CancellationTokenSource? _photoOperationCancellation;
    private Task? _persistTask;
    private Task? _photoTask;
    private Task? _leaveTask;
    private bool _hasLeft;

    public ImportViewModel(
        IAnglerPreferencesService preferences,
        IImportCatchProposalService catchProposals,
        IImportTripProposalService tripProposals,
        IImportExistingTripService existingTrips,
        IImportLocationLookupService locations,
        IImportPhotoPreparationService preparation,
        IImportPersistenceService persistence,
        IPhotographMediaAdapter media,
        IPhotographLocalTimeMapper localTime,
        IConnectivityService connectivity,
        IImportModalService modals,
        IMeasurementService measurement,
        IAccountUnitsService units,
        UiTextService text)
    {
        _preferences = preferences;
        _catchProposals = catchProposals;
        _tripProposals = tripProposals;
        _existingTrips = existingTrips;
        _locations = locations;
        _preparation = preparation;
        _persistence = persistence;
        _media = media;
        _localTime = localTime;
        _connectivity = connectivity;
        _modals = modals;
        _measurement = measurement;
        _units = units;
        Text = text;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UiTextService Text { get; }

    public ObservableCollection<ImportOptionModel> MethodOptions { get; } = [];

    public ObservableCollection<ImportOptionModel> SpeciesOptions { get; } = [];

    public ObservableCollection<ImportPhotoRowModel> Photos { get; } = [];

    public ObservableCollection<ImportCatchRowModel> Catches { get; } = [];

    public ObservableCollection<ImportTripRowModel> Trips { get; } = [];

    public ImportStageEnum Stage => _batch.Stage;

    public int StepNumber => VisibleStep(_batch.Stage);

    public int StepCount => 5;

    public string StepLabel => string.Format(Text.Culture, Text["OnboardingStepOf"], StepNumber, StepCount);

    public string StepTitle => _batch.Stage switch
    {
        ImportStageEnum.BatchDetails => Text["Import_BatchDetailsTitle"],
        ImportStageEnum.ChoosePhotos => Text["Import_ChoosePhotosTitle"],
        ImportStageEnum.ReviewCatches => Text["Import_ReviewTitle"],
        ImportStageEnum.ReviewTrips => Text["Import_TripSuggestionsTitle"],
        _ => Text["Import_ConfirmTitle"]
    };

    public string StepDescription => _isPersisted
        ? string.Format(
            Text.Culture,
            Text["Import_Success"],
            CountPhrase(ActiveCatches.Count, "Import_SuccessCatchOne", "Import_SuccessCatchOther"),
            CountPhrase(ActivePhotoCount, "Import_SuccessPhotoOne", "Import_SuccessPhotoOther"),
            CountPhrase(NewTripCount, "Import_SuccessTripOne", "Import_SuccessTripOther"))
        : Text["Import_NotSaved"];

    public string ContinueText => _batch.Stage switch
    {
        ImportStageEnum.ReviewCatches => Text["Import_ContinueToTrips"],
        ImportStageEnum.Confirm => _isPersisted ? Text["Import_ViewCatches"] : Text["Import_ConfirmImport"],
        _ => Text["Import_ReviewCatches"]
    };

    public bool CanGoBack => !_isPersisted && _batch.Stage != ImportStageEnum.BatchDetails && !_isPersisting;

    public bool CanContinue => !_isBusy && !_isPersisting && _batch.Stage switch
    {
        ImportStageEnum.BatchDetails => _batch.CanProcessPhotos,
        ImportStageEnum.ChoosePhotos => CanReviewPhotos,
        ImportStageEnum.ReviewCatches => _batch.CanAdvanceToTrips,
        ImportStageEnum.ReviewTrips => _batch.IsReadyForConfirmation,
        ImportStageEnum.Confirm => _isPersisted || _batch.IsReadyForConfirmation,
        _ => false
    };

    public bool HasContinueHint => !string.IsNullOrWhiteSpace(ContinueHint);

    public string ContinueHint
    {
        get
        {
            if (CanContinue || _isBusy || _isPersisting || _isPersisted)
            {
                return string.Empty;
            }

            return _batch.Stage switch
            {
                ImportStageEnum.ChoosePhotos => Text["Import_ChoosePhotosHint"],
                ImportStageEnum.ReviewCatches => CatchContinueHint(),
                ImportStageEnum.ReviewTrips => Text["Import_ChooseTripHint"],
                _ => string.Empty
            };
        }
    }

    public bool IsBusy => _isBusy || _isLoading || _isPersisting;

    public bool IsBatchStep => _batch.Stage == ImportStageEnum.BatchDetails;

    public bool IsPhotosStep => _batch.Stage == ImportStageEnum.ChoosePhotos;

    public bool IsCatchReviewStep => _batch.Stage == ImportStageEnum.ReviewCatches;

    public bool IsTripReviewStep => _batch.Stage == ImportStageEnum.ReviewTrips;

    public bool IsConfirmStep => _batch.Stage == ImportStageEnum.Confirm && !_isPersisted;

    public bool IsSuccessStep => _isPersisted;

    public bool IsPersisting => _isPersisting;

    public bool HasPhotos => Photos.Count > 0;

    public bool HasNoTrips => IsTripReviewStep && Trips.Count == 0;

    public bool HasMessage => !string.IsNullOrWhiteSpace(_message);

    public string? Message => _message;

    public bool SelectionLimitExceeded => _selectionLimitExceeded;

    public string SelectionLimitText => string.Format(
        Text.Culture,
        Text["Import_SelectionLimit"],
        ImportPhotoPreparationService.MaxPhotographs);

    public string ProgressText => ProgressLabel();

    public string SummaryCatches => string.Format(Text.Culture, Text["Import_SummaryCatches"], ActiveCatches.Count);

    public string SummaryPhotos => string.Format(Text.Culture, Text["Import_SummaryPhotos"], ActivePhotoCount);

    public string SummaryNewTrips => string.Format(Text.Culture, Text["Import_SummaryNewTrips"], NewTripCount);

    public string SummaryExistingTrips => string.Format(
        Text.Culture,
        Text["Import_SummaryExistingTrips"],
        ExistingTripAssignmentCount);

    public string SummaryStandalone => string.Format(
        Text.Culture,
        Text["Import_SummaryStandalone"],
        StandaloneCatchCount);

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        _isLoading = true;
        Notify();
        try
        {
            var loaded = await _preferences.RefreshAsync(cancellationToken);
            _preferencesModel = loaded.IsSuccess && loaded.Value is not null
                ? loaded.Value
                : await _preferences.GetCachedAsync(cancellationToken) ?? AnglerPreferencesModel.Empty;
            var units = await _units.GetAsync(cancellationToken);
            _weightUnit = units.WeightUnit;
            _lengthUnit = units.LengthUnit;
            ApplyPreferredDefaults();
            _batch = new ImportBatchModel(Guid.NewGuid(), SelectedMethod(), SelectedSpecies());
        }
        finally
        {
            _isLoading = false;
            Refresh();
        }
    }

    public CataloguePickerViewModel CreateMethodPicker()
    {
        return new CataloguePickerViewModel(
            Text,
            Text["Import_FishingMethod"],
            _preferencesModel.Catalogue.Methods.Select(method => new CataloguePickerOptionModel(
                method.Id,
                method.Name,
                method.Name,
                method.Id == _methodId)).ToArray(),
            allowMultiple: false);
    }

    public CataloguePickerViewModel CreateSpeciesPicker()
    {
        return new CataloguePickerViewModel(
            Text,
            Text["Import_Species"],
            _preferencesModel.Catalogue.AllSpecies.Select(species => new CataloguePickerOptionModel(
                species.Id,
                species.Name,
                species.Name,
                species.Id == _speciesId)).ToArray(),
            allowMultiple: false);
    }

    public void ApplyMethodPicker(CataloguePickerResultModel result)
    {
        var chosen = _preferencesModel.Catalogue.Methods.FirstOrDefault(method => result.SelectedIds.Contains(method.Id));
        if (chosen is not null)
        {
            SelectMethod(chosen.Id);
        }
    }

    public void ApplySpeciesPicker(CataloguePickerResultModel result)
    {
        var chosen = _preferencesModel.Catalogue.AllSpecies.FirstOrDefault(species => result.SelectedIds.Contains(species.Id));
        if (chosen is not null)
        {
            SelectSpecies(chosen.Id);
        }
    }

    public bool ChooseMethod(ImportOptionModel? option)
    {
        return ChooseChip(option, SelectMethod);
    }

    public bool ChooseSpecies(ImportOptionModel? option)
    {
        return ChooseChip(option, SelectSpecies);
    }

    public void CompleteMethodPicker(CataloguePickerResultModel? result)
    {
        if (result is not null)
        {
            ApplyMethodPicker(result);
        }
    }

    public void CompleteSpeciesPicker(CataloguePickerResultModel? result)
    {
        if (result is not null)
        {
            ApplySpeciesPicker(result);
        }
    }

    public CataloguePickerViewModel CreateCatchMethodPicker(Guid catchId)
    {
        var selected = _batch.CatchProposals.Single(proposal => proposal.Id == catchId).Method.Id;
        return new CataloguePickerViewModel(
            Text,
            Text["Import_FishingMethod"],
            _preferencesModel.Catalogue.Methods.Select(method => new CataloguePickerOptionModel(
                method.Id,
                method.Name,
                method.Name,
                method.Id == selected)).ToArray(),
            allowMultiple: false);
    }

    public CataloguePickerViewModel CreateCatchSpeciesPicker(Guid catchId)
    {
        var selected = _batch.CatchProposals.Single(proposal => proposal.Id == catchId).Species.Id;
        return new CataloguePickerViewModel(
            Text,
            Text["Import_Species"],
            _preferencesModel.Catalogue.AllSpecies.Select(species => new CataloguePickerOptionModel(
                species.Id,
                species.Name,
                species.Name,
                species.Id == selected)).ToArray(),
            allowMultiple: false);
    }

    public bool ChooseCatchMethod(ImportOptionModel? option)
    {
        return ChooseCatchChip(option, SetCatchMethod);
    }

    public bool ChooseCatchSpecies(ImportOptionModel? option)
    {
        return ChooseCatchChip(option, SetCatchSpecies);
    }

    public void CompleteCatchMethodPicker(Guid catchId, CataloguePickerResultModel? result)
    {
        var chosen = result is null
            ? null
            : _preferencesModel.Catalogue.Methods.FirstOrDefault(method => result.SelectedIds.Contains(method.Id));
        if (chosen is not null)
        {
            SetCatchMethod(catchId, chosen.Id);
        }
    }

    public void CompleteCatchSpeciesPicker(Guid catchId, CataloguePickerResultModel? result)
    {
        var chosen = result is null
            ? null
            : _preferencesModel.Catalogue.AllSpecies.FirstOrDefault(species => result.SelectedIds.Contains(species.Id));
        if (chosen is not null)
        {
            SetCatchSpecies(catchId, chosen.Id);
        }
    }

    public MeasurementEditorViewModel CreateWeightEditor(Guid catchId)
    {
        return new MeasurementEditorViewModel(
            _measurement,
            Text,
            MeasurementKindEnum.Weight,
            _batch.CatchProposals.Single(proposal => proposal.Id == catchId).Weight,
            _weightUnit,
            _lengthUnit);
    }

    public MeasurementEditorViewModel CreateLengthEditor(Guid catchId)
    {
        return new MeasurementEditorViewModel(
            _measurement,
            Text,
            MeasurementKindEnum.Length,
            _batch.CatchProposals.Single(proposal => proposal.Id == catchId).Length,
            _weightUnit,
            _lengthUnit);
    }

    public void CompleteWeightEdit(Guid catchId, bool applied, MeasurementEditorViewModel editor)
    {
        if (!applied)
        {
            return;
        }

        _batch.SetCatchWeight(catchId, editor.CanonicalValue);
        Refresh();
    }

    public void CompleteLengthEdit(Guid catchId, bool applied, MeasurementEditorViewModel editor)
    {
        if (!applied)
        {
            return;
        }

        _batch.SetCatchLength(catchId, editor.CanonicalValue);
        Refresh();
    }

    public void SelectMethod(Guid methodId)
    {
        _methodId = methodId;
        ApplyDefaults();
        Refresh();
    }

    public void SelectSpecies(Guid speciesId)
    {
        _speciesId = speciesId;
        ApplyDefaults();
        Refresh();
    }

    public async Task ContinueAsync(CancellationToken cancellationToken)
    {
        if (_hasLeft || !CanContinue)
        {
            return;
        }

        switch (_batch.Stage)
        {
            case ImportStageEnum.BatchDetails:
                _batch.SetStage(ImportStageEnum.ChoosePhotos);
                break;
            case ImportStageEnum.ChoosePhotos:
                await ContinueToReviewAsync(cancellationToken);
                break;
            case ImportStageEnum.ReviewCatches:
                await ContinueToTripsAsync(cancellationToken);
                break;
            case ImportStageEnum.ReviewTrips:
                _batch.SetStage(ImportStageEnum.Confirm);
                break;
            case ImportStageEnum.Confirm when _isPersisted:
                break;
            case ImportStageEnum.Confirm:
                await PersistAsync(cancellationToken);
                break;
        }

        Refresh();
    }

    public void Back()
    {
        if (!CanGoBack)
        {
            return;
        }

        _batch.SetStage(_batch.Stage switch
        {
            ImportStageEnum.ChoosePhotos => ImportStageEnum.BatchDetails,
            ImportStageEnum.ReviewCatches => ImportStageEnum.ChoosePhotos,
            ImportStageEnum.ReviewTrips => ImportStageEnum.ReviewCatches,
            _ => ImportStageEnum.ReviewCatches
        });
        Refresh();
    }

    public async Task PickPhotosAsync(CancellationToken cancellationToken)
    {
        if (!_batch.CanProcessPhotos || _isPersisting || _hasLeft)
        {
            return;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _photoOperationCancellation = linked;
        var operation = PickPhotosCoreAsync(linked.Token);
        _photoTask = operation;
        try
        {
            await operation;
        }
        finally
        {
            if (ReferenceEquals(_photoTask, operation))
            {
                _photoTask = null;
            }

            if (ReferenceEquals(_photoOperationCancellation, linked))
            {
                _photoOperationCancellation = null;
            }
        }
    }

    private async Task PickPhotosCoreAsync(CancellationToken cancellationToken)
    {
        var picked = await _media.PickPhotosAsync(ImportPhotoPreparationService.MaxPhotographs, cancellationToken);
        if (_hasLeft || cancellationToken.IsCancellationRequested || picked.Outcome == PhotographPickOutcomeEnum.Cancelled)
        {
            return;
        }

        if (picked.Outcome != PhotographPickOutcomeEnum.Selected)
        {
            _message = Text["Import_PhotoFailed"];
            Refresh();
            return;
        }

        if (picked.Files.Count > ImportPhotoPreparationService.MaxPhotographs)
        {
            _selectionLimitExceeded = true;
            Refresh();
            return;
        }

        _isBusy = true;
        _selectionLimitExceeded = false;
        _message = null;
        Refresh();
        try
        {
            _batch.BeginPhotoProcessing();
            var photos = await _preparation.PrepareSelectionAsync(picked.Files, cancellationToken);
            if (_hasLeft || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _batch = new ImportBatchModel(_batch.Id, SelectedMethod(), SelectedSpecies());
            foreach (var photo in photos)
            {
                _batch.AddPhoto(photo);
            }

            _batch.SetStage(ImportStageEnum.ChoosePhotos);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || _hasLeft)
        {
        }
        catch (Exception)
        {
            if (!_hasLeft)
            {
                _message = Text["Import_PhotoFailed"];
            }
        }
        finally
        {
            _batch.CompletePhotoProcessing();
            _isBusy = false;
            Refresh();
        }
    }

    public async Task RemovePhotoAsync(Guid photoId, CancellationToken cancellationToken)
    {
        var photo = _batch.Photos.SingleOrDefault(candidate => candidate.Id == photoId);
        if (photo is null)
        {
            return;
        }

        await _preparation.RemoveAsync(photo, cancellationToken);
        _batch.RemovePhoto(photoId);
        Refresh();
    }

    public void KeepDuplicate(Guid photoId)
    {
        _batch.KeepDuplicatePhoto(photoId);
        Refresh();
    }

    public void ConfirmCatch(Guid catchId)
    {
        _batch.ConfirmDisplayedCatch(catchId);
        Refresh();
    }

    public void UseShownValues(Guid catchId)
    {
        _batch.CatchProposals.Single(proposal => proposal.Id == catchId).ConfirmCanonicalValues();
        Refresh();
    }

    public async Task ConfirmCaughtOnAsync(Guid catchId, DateTime localValue, CancellationToken cancellationToken)
    {
        var proposal = _batch.CatchProposals.Single(candidate => candidate.Id == catchId);
        var current = CaughtOnLocal(proposal.CaughtOn);
        if (proposal.CaughtOn.IsResolved
            && current.Year == localValue.Year
            && current.Month == localValue.Month
            && current.Day == localValue.Day
            && current.Hour == localValue.Hour
            && current.Minute == localValue.Minute)
        {
            return;
        }

        if (proposal.CaughtOn.Instant.HasValue)
        {
            _batch.SetCatchCaughtOn(catchId, proposal.CaughtOn.Confirm(localValue));
            Refresh();
            return;
        }

        var formatted = DateTime.SpecifyKind(localValue, DateTimeKind.Unspecified).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        var instant = await _localTime.FromDateTimeLocalValueAsync(formatted, cancellationToken);
        if (instant is null)
        {
            _message = Text["Import_CaughtOnRequired"];
            Refresh();
            return;
        }

        _batch.SetCatchCaughtOn(catchId, ImportTimestampModel.UserConfirmed(instant.Value));
        Refresh();
    }

    public void AcceptLocation(Guid catchId, ImportLocationModel location)
    {
        _batch.SetCatchLocation(catchId, location.Accept());
        Refresh();
    }

    public void RemoveLocation(Guid catchId)
    {
        var current = _batch.CatchProposals.Single(proposal => proposal.Id == catchId).Location;
        if (current is not null)
        {
            _batch.SetCatchLocation(catchId, current.Remove());
        }

        Refresh();
    }

    public async Task EditPlaceNameAsync(Guid catchId)
    {
        var proposal = _batch.CatchProposals.Single(candidate => candidate.Id == catchId);
        var components = proposal.Location?.LookupResult?.Components ?? [];
        if (components.Count == 0 && !string.IsNullOrWhiteSpace(proposal.Location?.PlaceName))
        {
            components = proposal.Location.PlaceName.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        }

        if (proposal.Location is null || components.Count == 0)
        {
            return;
        }

        var result = await _modals.EditPlaceNameAsync(new PlaceNameModalModel(components, proposal.Location.PlaceName));
        if (result is not null)
        {
            _batch.SetCatchLocation(catchId, proposal.Location.WithPlaceName(result.PlaceName));
            Refresh();
        }
    }

    public void TogglePhotoSelection(Guid catchId, Guid photoId)
    {
        if (!_selectedPhotos.TryGetValue(catchId, out var selected))
        {
            selected = [];
            _selectedPhotos[catchId] = selected;
        }

        if (!selected.Add(photoId))
        {
            selected.Remove(photoId);
        }

        Refresh();
    }

    public void SplitSelectedPhotos(Guid catchId)
    {
        if (!_selectedPhotos.TryGetValue(catchId, out var selected) || selected.Count == 0)
        {
            return;
        }

        _batch.SplitCatch(catchId, selected.ToArray(), Guid.NewGuid());
        _selectedPhotos.Remove(catchId);
        Refresh();
    }

    public async Task RemoveSelectedPhotosAsync(Guid catchId, CancellationToken cancellationToken)
    {
        if (!_selectedPhotos.TryGetValue(catchId, out var selected) || selected.Count == 0)
        {
            return;
        }

        foreach (var photoId in selected.ToArray())
        {
            await RemovePhotoAsync(photoId, cancellationToken);
        }

        _selectedPhotos.Remove(catchId);
        Refresh();
    }

    public void MergeCatches(Guid primaryId, Guid absorbedId)
    {
        _batch.MergeCatches(primaryId, absorbedId);
        Refresh();
    }

    public async Task RemoveCatchAsync(Guid catchId, CancellationToken cancellationToken)
    {
        var proposal = _batch.CatchProposals.Single(candidate => candidate.Id == catchId);
        foreach (var photoId in proposal.PhotoIds.ToArray())
        {
            var photo = _batch.Photos.Single(candidate => candidate.Id == photoId);
            await _preparation.RemoveAsync(photo, cancellationToken);
        }

        _batch.RemoveCatchProposal(catchId);
        Refresh();
    }

    public void SetCatchMethod(Guid catchId, Guid methodId)
    {
        var method = FindMethod(methodId);
        if (method is not null)
        {
            _batch.SetCatchMethod(catchId, Selection(method));
            Refresh();
        }
    }

    public void SetCatchSpecies(Guid catchId, Guid speciesId)
    {
        var species = FindSpecies(speciesId);
        if (species is not null)
        {
            _batch.SetCatchSpecies(catchId, Selection(species));
            Refresh();
        }
    }

    public void DecideCreateTrip(Guid tripId)
    {
        _batch.DecideTrip(tripId, ImportTripDecisionEnum.CreateNew);
        Refresh();
    }

    public void DecideKeepSeparate(Guid tripId)
    {
        _batch.DecideTrip(tripId, ImportTripDecisionEnum.NoTrip);
        Refresh();
    }

    public async Task DecideExistingTripAsync(Guid tripId)
    {
        var candidates = _existingTripLookup.GetValueOrDefault(tripId) ?? [];
        if (candidates.Count == 0)
        {
            return;
        }

        var chosen = await _modals.ChooseExistingTripAsync(candidates);
        if (chosen.HasValue)
        {
            _batch.DecideTrip(tripId, ImportTripDecisionEnum.UseExisting, chosen.Value);
            Refresh();
        }
    }

    public void RemoveCatchFromTrip(Guid tripId, Guid catchId)
    {
        _batch.RemoveCatchFromTrip(tripId, catchId);
        Refresh();
    }

    public async Task AddTripParticipantAsync(Guid tripId)
    {
        var proposal = _batch.TripProposals.SingleOrDefault(candidate => candidate.Id == tripId);
        if (proposal is null || proposal.Decision != ImportTripDecisionEnum.CreateNew || _isPersisting)
        {
            return;
        }

        if (!_connectivity.IsOnline)
        {
            _message = Text["Import_OnlineRequired"];
            Refresh();
            return;
        }

        var chosen = await _modals.ChooseAnglerAsync(
            proposal.Participants.Select(participant => participant.UserId).ToArray());
        if (chosen is not null)
        {
            _batch.AddTripParticipant(tripId, chosen);
            Refresh();
        }
    }

    public void RemoveTripParticipant(Guid tripId, Guid userId)
    {
        _batch.RemoveTripParticipant(tripId, userId);
        Refresh();
    }

    public void CancelPersistence()
    {
        _persistenceCancellation?.Cancel();
    }

    public Task LeaveAsync()
    {
        if (_isPersisted)
        {
            return Task.CompletedTask;
        }

        return _leaveTask ??= LeaveCoreAsync();
    }

    private async Task LeaveCoreAsync()
    {
        _hasLeft = true;
        _photoOperationCancellation?.Cancel();
        _persistenceCancellation?.Cancel();
        await AwaitCancelledAsync(_photoTask);
        await AwaitCancelledAsync(_persistTask);
        if (_isPersisted)
        {
            return;
        }

        await _preparation.ClearAsync(CancellationToken.None);
    }

    private static async Task AwaitCancelledAsync(Task? operation)
    {
        if (operation is null)
        {
            return;
        }

        try
        {
            await operation;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ContinueToReviewAsync(CancellationToken cancellationToken)
    {
        _isBusy = true;
        Refresh();
        try
        {
            await _locations.ResolveAsync(_batch.Photos, cancellationToken);
            _batch.ReplaceCatchProposals(_catchProposals.Propose(_batch));
            await ApplyDisplayedCaughtOnAsync(cancellationToken);
            _batch.SetStage(ImportStageEnum.ReviewCatches);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task ContinueToTripsAsync(CancellationToken cancellationToken)
    {
        _isBusy = true;
        Refresh();
        try
        {
            _batch.ReplaceTripProposals(_tripProposals.Propose(_batch));
            _existingTripLookup = await _existingTrips.GetCandidatesAsync(
                _batch.TripProposals.Where(proposal => !proposal.IsRemoved).ToArray(),
                cancellationToken);
            _batch.SetStage(ImportStageEnum.ReviewTrips);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        if (_isPersisting || _isPersisted || _hasLeft)
        {
            return;
        }

        _isPersisting = true;
        _message = null;
        _progress = null;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _persistenceCancellation = linked;
        var persist = PersistCoreAsync(linked.Token);
        _persistTask = persist;
        Refresh();
        try
        {
            await persist;
        }
        finally
        {
            if (ReferenceEquals(_persistTask, persist))
            {
                _persistTask = null;
            }

            _isPersisting = false;
            _persistenceCancellation = null;
            Refresh();
        }
    }

    private async Task PersistCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!_connectivity.IsOnline)
            {
                _message = Text["Import_OnlineRequired"];
                return;
            }

            var result = await _persistence.PersistAsync(
                _batch,
                cancellationToken,
                new Progress<ImportPersistenceProgressModel>(progress =>
                {
                    _progress = progress;
                    Notify(nameof(ProgressText), nameof(IsBusy));
                }));
            if (!result.IsSuccess)
            {
                _message = PersistenceFailureLabel(result.Failure);
                return;
            }

            _isPersisted = true;
            await _preparation.ClearAsync(CancellationToken.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _message = null;
        }
        catch (Exception)
        {
            _message = Text["Import_PersistenceFailed"];
        }
    }

    private bool CanReviewPhotos =>
        !_batch.IsProcessingPhotos
        && _batch.Photos.Any(photo => !photo.IsRemoved && photo.IsReady)
        && !_batch.HasUnresolvedExactDuplicates;

    private IReadOnlyList<ImportCatchProposalModel> ActiveCatches =>
        [.. _batch.CatchProposals.Where(proposal => !proposal.IsRemoved)];

    private IReadOnlyList<ImportTripProposalModel> ActiveTrips =>
        [.. _batch.TripProposals.Where(proposal => !proposal.IsRemoved)];

    private int ActivePhotoCount => ActiveCatches.Sum(proposal => proposal.PhotoIds.Count);

    private int NewTripCount => ActiveTrips.Count(proposal => proposal.Decision == ImportTripDecisionEnum.CreateNew);

    private int ExistingTripAssignmentCount =>
        ActiveTrips.Count(proposal => proposal.Decision == ImportTripDecisionEnum.UseExisting);

    private int StandaloneCatchCount
    {
        get
        {
            var assigned = ActiveTrips.SelectMany(proposal => proposal.CatchProposalIds).ToHashSet();
            return ActiveCatches.Count(proposal => !assigned.Contains(proposal.Id));
        }
    }

    private void ApplyPreferredDefaults()
    {
        var method = _preferencesModel.Preferences.Methods.FirstOrDefault(candidate => candidate.IsDefault);
        if (method is null)
        {
            return;
        }

        _methodId = method.FishingMethodId;
        _speciesId = method.Species.FirstOrDefault(candidate => candidate.IsDefault)?.SpeciesId ?? Guid.Empty;
        ApplyDefaults();
    }

    private void ApplyDefaults()
    {
        _batch.SetDefaults(SelectedMethod(), SelectedSpecies());
    }

    private ImportCatalogueSelectionModel SelectedMethod()
    {
        var method = FindMethod(_methodId);
        return method is null ? EmptySelection() : Selection(method);
    }

    private ImportCatalogueSelectionModel SelectedSpecies()
    {
        var species = FindSpecies(_speciesId);
        return species is null ? EmptySelection() : Selection(species);
    }

    private FishingMethodDto? FindMethod(Guid id)
    {
        return _preferencesModel.Catalogue.Methods.SingleOrDefault(candidate => candidate.Id == id);
    }

    private SpeciesDto? FindSpecies(Guid id)
    {
        return _preferencesModel.Catalogue.AllSpecies.SingleOrDefault(candidate => candidate.Id == id);
    }

    private static ImportCatalogueSelectionModel Selection(FishingMethodDto method)
    {
        return new ImportCatalogueSelectionModel(method.Id, method.Code, method.Name);
    }

    private static ImportCatalogueSelectionModel Selection(SpeciesDto species)
    {
        return new ImportCatalogueSelectionModel(species.Id, species.Code, species.Name);
    }

    private static ImportCatalogueSelectionModel EmptySelection()
    {
        return new ImportCatalogueSelectionModel(Guid.Empty, string.Empty, string.Empty);
    }

    private void Refresh()
    {
        Replace(MethodOptions, BuildMethodOptions());
        Replace(SpeciesOptions, BuildSpeciesOptions());
        Replace(Photos, _batch.Photos.Where(photo => !photo.IsRemoved).Select(ToPhotoRow));
        Replace(Catches, ActiveCatches.Select((proposal, index) => ToCatchRow(proposal, index + 1)));
        Replace(Trips, ActiveTrips.Select(ToTripRow));
        Notify();
    }

    private IReadOnlyList<ImportOptionModel> BuildMethodOptions()
    {
        var preferredIds = _preferencesModel.Preferences.Methods
            .OrderByDescending(method => method.IsDefault)
            .Select(method => method.FishingMethodId)
            .ToArray();
        var methods = preferredIds.Length > 0
            ? preferredIds.Select(FindMethod).Where(method => method is not null).Select(method => method!)
            : _preferencesModel.Catalogue.Methods;
        var selected = FindMethod(_methodId);
        return BuildShortlist(
            methods.Select(method => new ImportOptionModel(method.Id, method.Code, method.Name, method.Id == _methodId)),
            selected is null ? null : new ImportOptionModel(selected.Id, selected.Code, selected.Name, true));
    }

    private IReadOnlyList<ImportOptionModel> BuildSpeciesOptions()
    {
        var preferred = _preferencesModel.Preferences.Methods
            .FirstOrDefault(method => method.FishingMethodId == _methodId)?
            .Species
            .OrderByDescending(species => species.IsDefault)
            .Select(species => FindSpecies(species.SpeciesId))
            .Where(species => species is not null)
            .Select(species => species!)
            .ToArray() ?? [];
        var selected = FindSpecies(_speciesId);
        return BuildShortlist(
            preferred.Select(species => new ImportOptionModel(species.Id, species.Code, species.Name, species.Id == _speciesId)),
            selected is null ? null : new ImportOptionModel(selected.Id, selected.Code, selected.Name, true));
    }

    private IReadOnlyList<ImportOptionModel> BuildShortlist(
        IEnumerable<ImportOptionModel> options,
        ImportOptionModel? selected)
    {
        var shortlist = options.Take(MaxChipOptions).ToList();
        if (selected is not null && shortlist.All(option => option.Id != selected.Id))
        {
            shortlist.Insert(0, selected);
        }

        shortlist.Add(new ImportOptionModel(Guid.Empty, MoreOptionCode, Text["More"], false, selected?.CatchId));
        return shortlist;
    }

    private IReadOnlyList<ImportOptionModel> BuildCatchMethodOptions(ImportCatchProposalModel proposal)
    {
        return BuildMethodOptions()
            .Select(option => option with
            {
                IsSelected = option.Code != MoreOptionCode && option.Id == proposal.Method.Id,
                CatchId = proposal.Id
            })
            .ToArray();
    }

    private IReadOnlyList<ImportOptionModel> BuildCatchSpeciesOptions(ImportCatchProposalModel proposal)
    {
        return BuildSpeciesOptions()
            .Select(option => option with
            {
                IsSelected = option.Code != MoreOptionCode && option.Id == proposal.Species.Id,
                CatchId = proposal.Id
            })
            .ToArray();
    }

    private bool ChooseChip(ImportOptionModel? option, Action<Guid> select)
    {
        if (option is null)
        {
            return false;
        }

        if (string.Equals(option.Code, MoreOptionCode, StringComparison.Ordinal))
        {
            return true;
        }

        select(option.Id);
        return false;
    }

    private bool ChooseCatchChip(ImportOptionModel? option, Action<Guid, Guid> select)
    {
        if (option?.CatchId is not Guid catchId)
        {
            return false;
        }

        if (string.Equals(option.Code, MoreOptionCode, StringComparison.Ordinal))
        {
            return true;
        }

        select(catchId, option.Id);
        return false;
    }

    private static DateTime CaughtOnLocal(ImportTimestampModel timestamp)
    {
        if (timestamp.Instant is { } instant)
        {
            return DateTime.SpecifyKind(instant.DateTime, DateTimeKind.Unspecified);
        }

        if (timestamp.LocalWallClock is { } wallClock)
        {
            return DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);
        }

        return DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
    }

    private string FormatMeasurement(decimal? canonical, bool weight)
    {
        var label = Text[weight ? "MeasureWeight" : "MeasureLength"];
        if (canonical is null)
        {
            return $"{label} · {Text["MeasureNotRecorded"]}";
        }

        if (weight)
        {
            var display = _measurement.ToDisplayWeight(canonical, _weightUnit);
            var unit = Text[_weightUnit == WeightUnitEnum.Lb ? "MeasureUnitLb" : "MeasureUnitKg"];
            return display is null
                ? $"{label} · {Text["MeasureNotRecorded"]}"
                : $"{label} · {display.Value.ToString("0.##", Text.Culture)} {unit}";
        }

        var length = _measurement.ToDisplayLength(canonical, _lengthUnit);
        var lengthUnit = Text[_lengthUnit == LengthUnitEnum.In ? "MeasureUnitIn" : "MeasureUnitCm"];
        return length is null
            ? $"{label} · {Text["MeasureNotRecorded"]}"
            : $"{label} · {length.Value.ToString("0.##", Text.Culture)} {lengthUnit}";
    }

    private ImportPhotoRowModel ToPhotoRow(ImportSelectedPhotoModel photo)
    {
        var status = photo.IsReady
            ? PhotoMetadataLabel(photo)
            : PhotoPreparationLabel(photo);
        var duplicate = photo.DuplicateStatus switch
        {
            ImportDuplicateStatusEnum.Duplicate => Text["Import_DuplicateExact"],
            ImportDuplicateStatusEnum.Warning => Text["Import_DuplicatePossible"],
            _ => string.Empty
        };
        return new ImportPhotoRowModel(
            photo,
            status,
            duplicate,
            photo.RequiresDuplicateDecision
            && _batch.Photos.Any(candidate => !candidate.IsRemoved && photo.DuplicatePhotoIds.Contains(candidate.Id)),
            photo.DuplicateStatus == ImportDuplicateStatusEnum.Warning);
    }

    private ImportCatchRowModel ToCatchRow(ImportCatchProposalModel proposal, int number)
    {
        var selected = _selectedPhotos.GetValueOrDefault(proposal.Id) ?? [];
        var photos = proposal.PhotoIds
            .Select(photoId => _batch.Photos.Single(photo => photo.Id == photoId))
            .OrderBy(photo => photo.SelectionIndex)
            .Select((photo, index) => new ImportCatchPhotoModel(
                proposal.Id,
                photo,
                selected.Contains(photo.Id),
                string.Format(Text.Culture, Text["Import_PhotoNumber"], index + 1)))
            .ToArray();
        var locations = photos
            .Where(photo => photo.Photo.Location.HasCanonicalCoordinates)
            .DistinctBy(photo => (photo.Photo.Location.Latitude, photo.Photo.Location.Longitude))
            .Select((photo, index) => new ImportLocationChoiceModel(
                proposal.Id,
                photo.Photo.Id,
                string.Format(Text.Culture, Text["Import_UsePhotoLocation"], index + 1),
                photo.Photo.Location))
            .ToArray();
        var mergeTargets = ActiveCatches
            .Where(candidate => candidate.Id != proposal.Id)
            .Select(candidate => new ImportMergeTargetModel(
                proposal.Id,
                candidate.Id,
                string.Format(Text.Culture, Text["Import_CatchNumber"], ActiveCatches.ToList().IndexOf(candidate) + 1)))
            .ToArray();
        var caughtOn = CaughtOnLocal(proposal.CaughtOn);
        return new ImportCatchRowModel(
            proposal,
            number,
            string.Format(Text.Culture, Text["Import_CatchNumber"], number),
            CatchStatusText(proposal),
            TimestampText(proposal.CaughtOn),
            LocationText(proposal),
            proposal.Method.Name,
            proposal.Species.Name,
            photos,
            locations,
            mergeTargets,
            BuildCatchMethodOptions(proposal),
            BuildCatchSpeciesOptions(proposal),
            caughtOn.Date,
            caughtOn.TimeOfDay,
            FormatMeasurement(proposal.Weight, true),
            FormatMeasurement(proposal.Length, false),
            Text.Culture.DateTimeFormat.ShortDatePattern,
            Text.Culture.DateTimeFormat.ShortTimePattern,
            proposal.Location?.HasCanonicalCoordinates == true
                ? string.Empty
                : Text["Import_LocationUnavailableHelp"]);
    }

    private ImportTripRowModel ToTripRow(ImportTripProposalModel proposal)
    {
        var count = proposal.CatchProposalIds.Count;
        var title = count == 1
            ? Text["Import_TripSingleCatch"]
            : string.Format(Text.Culture, Text["Import_TripCatchCount"], count);
        var range = string.Format(
            Text.Culture,
            Text["Import_TripTimeRange"],
            proposal.ProposedStartedOn.ToString("g", Text.Culture),
            proposal.ProposedEndedOn.ToString("g", Text.Culture));
        var existing = _existingTripLookup.GetValueOrDefault(proposal.Id) ?? [];
        var catches = proposal.CatchProposalIds
            .Select((catchId, index) => new ImportTripCatchModel(
                proposal.Id,
                catchId,
                string.Format(Text.Culture, Text["Import_TripCatchNumber"], index + 1)))
            .ToArray();
        var decisionText = proposal.Decision switch
        {
            ImportTripDecisionEnum.CreateNew => Text["Import_TripDecisionCreate"],
            ImportTripDecisionEnum.NoTrip => Text["Import_TripDecisionSeparate"],
            ImportTripDecisionEnum.UseExisting => Text["Import_TripDecisionExisting"],
            _ => string.Empty
        };
        var participants = proposal.Participants
            .Select(participant => new ImportTripParticipantModel(
                proposal.Id,
                participant.UserId,
                participant.DisplayName ?? participant.Email ?? Text["TripContributorUnknown"]))
            .ToArray();
        return new ImportTripRowModel(
            proposal,
            title,
            range,
            proposal.Reasons.Contains(ImportTripSuggestionReasonEnum.MissingGps) ? Text["Import_TripMissingGps"] : string.Empty,
            existing.Count > 0,
            existing,
            catches,
            existing.Count > 0 ? Text["Import_CreateAnotherTrip"] : Text["Import_CreateTrip"],
            decisionText,
            participants);
    }

    private string CatchStatusText(ImportCatchProposalModel proposal)
    {
        return proposal.ReviewStatus == ImportCatchReviewStatusEnum.Reviewed
            ? Text["Import_Confirmed"]
            : proposal.CanBeReviewed
                ? Text["Import_NeedsConfirmation"]
                : Text["Import_NeedsCorrection"];
    }

    private async Task ApplyDisplayedCaughtOnAsync(CancellationToken cancellationToken)
    {
        foreach (var proposal in _batch.CatchProposals.Where(candidate => !candidate.IsRemoved && !candidate.CaughtOn.IsResolved))
        {
            await ConfirmCaughtOnAsync(proposal.Id, CaughtOnLocal(proposal.CaughtOn), cancellationToken);
        }
    }

    private string CatchContinueHint()
    {
        var pending = ActiveCatches
            .Select((proposal, index) => (Proposal: proposal, Number: index + 1))
            .FirstOrDefault(candidate => !candidate.Proposal.IsReadyForConfirmation);
        if (pending.Proposal is null)
        {
            return string.Empty;
        }

        if (!pending.Proposal.CaughtOn.IsResolved)
        {
            return string.Format(Text.Culture, Text["Import_ConfirmDateHint"], pending.Number);
        }

        return pending.Proposal.CanBeReviewed
            ? string.Format(Text.Culture, Text["Import_ConfirmCatchHint"], pending.Number)
            : string.Format(Text.Culture, Text["Import_ReviewCatchHint"], pending.Number);
    }

    private string CountPhrase(int count, string oneKey, string otherKey)
    {
        return string.Format(Text.Culture, count == 1 ? Text[oneKey] : Text[otherKey], count);
    }

    private string TimestampText(ImportTimestampModel timestamp)
    {
        return timestamp.State switch
        {
            ImportTimestampStateEnum.ExplicitInstant => timestamp.Instant!.Value.ToString("g", Text.Culture),
            ImportTimestampStateEnum.UserConfirmed when timestamp.Instant is { } instant => instant.ToString("g", Text.Culture),
            ImportTimestampStateEnum.LocalWallClock => string.Format(
                Text.Culture,
                Text["Import_TimestampAmbiguous"],
                timestamp.LocalWallClock!.Value.ToString("g", Text.Culture)),
            ImportTimestampStateEnum.WeakFallback => string.Format(
                Text.Culture,
                Text["Import_TimestampWeak"],
                timestamp.Instant!.Value.ToString("g", Text.Culture)),
            ImportTimestampStateEnum.Unusable => Text["Import_TimestampUnusable"],
            _ => Text["Import_TimestampMissing"]
        };
    }

    private string LocationText(ImportCatchProposalModel proposal)
    {
        if (proposal.HasUnresolvedGpsConflict)
        {
            return Text["Import_LocationNeedsReview"];
        }

        if (proposal.Location?.Decision == ImportLocationDecisionEnum.Removed)
        {
            return Text["Import_LocationRemoved"];
        }

        if (!string.IsNullOrWhiteSpace(proposal.Location?.PlaceName))
        {
            return proposal.Location.PlaceName;
        }

        if (proposal.Location?.Decision == ImportLocationDecisionEnum.Accepted)
        {
            return Text["Import_LocationAccepted"];
        }

        return proposal.Location?.HasCanonicalCoordinates == true
            ? Text["Import_LocationAvailable"]
            : Text["Import_LocationUnavailable"];
    }

    private string PhotoPreparationLabel(ImportSelectedPhotoModel photo)
    {
        return photo.PreparationStatus switch
        {
            ImportPhotoPreparationStatusEnum.UnsupportedType => Text["Import_PhotoUnsupported"],
            ImportPhotoPreparationStatusEnum.TooLarge => Text["Import_PhotoTooLarge"],
            ImportPhotoPreparationStatusEnum.Cancelled => Text["Import_PhotoCancelled"],
            _ => Text["Import_PhotoFailed"]
        };
    }

    private string PhotoMetadataLabel(ImportSelectedPhotoModel photo)
    {
        return photo.MetadataStatus is ImportMetadataStatusEnum.Failed or ImportMetadataStatusEnum.Unavailable
            ? Text["Import_MetadataUnavailable"]
            : Text["Import_PhotoReady"];
    }

    private string ProgressLabel()
    {
        if (_progress is null)
        {
            return Text["Import_Importing"];
        }

        return _progress.Stage switch
        {
            ImportPersistenceStageEnum.SavingTrip => string.Format(
                Text.Culture,
                Text["Import_ProgressSavingTrip"],
                _progress.Current,
                _progress.Total),
            ImportPersistenceStageEnum.SavingCatch => string.Format(
                Text.Culture,
                Text["Import_ProgressSavingCatch"],
                _progress.Current,
                _progress.Total),
            ImportPersistenceStageEnum.UploadingPhotograph => string.Format(
                Text.Culture,
                Text["Import_ProgressUploadingPhotograph"],
                _progress.Current,
                _progress.Total),
            _ => Text["Import_ProgressVerifying"]
        };
    }

    private string PersistenceFailureLabel(ImportPersistenceFailureEnum? failure)
    {
        return failure switch
        {
            ImportPersistenceFailureEnum.Trip => Text["Import_TripPersistenceFailed"],
            ImportPersistenceFailureEnum.Catch => Text["Import_CatchPersistenceFailed"],
            ImportPersistenceFailureEnum.Photograph => Text["Import_PhotographPersistenceFailed"],
            _ => Text["Import_VerificationFailed"]
        };
    }

    private static int VisibleStep(ImportStageEnum stage)
    {
        return stage switch
        {
            ImportStageEnum.BatchDetails => 1,
            ImportStageEnum.ChoosePhotos => 2,
            ImportStageEnum.ReviewCatches => 3,
            ImportStageEnum.ReviewTrips => 4,
            _ => 5
        };
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private void Notify(params string[] names)
    {
        if (names.Length == 0)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            return;
        }

        foreach (var name in names)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
