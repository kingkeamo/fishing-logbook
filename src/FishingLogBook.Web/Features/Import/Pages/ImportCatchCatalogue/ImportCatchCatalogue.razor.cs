using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Common.Modals.AnglerPicker;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Import.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Import.Pages.ImportCatchCatalogue;

public partial class ImportCatchCatalogue : ComponentBase, IAsyncDisposable
{
    private const int MaxChipOptions = 6;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private AnglerPreferencesModel _preferences = AnglerPreferencesModel.Empty;
    private ImportBatchModel? _batch;
    private Guid _methodId;
    private Guid _speciesId;
    private bool _isLoading = true;
    private bool _selectionLimitExceeded;
    private bool _isPersisting;
    private bool _isPersisted;
    private string? _persistenceError;
    private ImportPersistenceResultModel? _persistenceResult;
    private ImportPersistenceProgressModel? _persistenceProgress;
    private IReadOnlyDictionary<Guid, IReadOnlyList<TripSummaryDto>> _existingTrips =
        new Dictionary<Guid, IReadOnlyList<TripSummaryDto>>();

    [Inject] private IAnglerPreferencesProvider Preferences { get; set; } = default!;
    [Inject] private IModalService ModalService { get; set; } = default!;
    [Inject] private IImportCatchProposalService ProposalService { get; set; } = default!;
    [Inject] private IImportPhotoPreparationService Preparation { get; set; } = default!;
    [Inject] private IImportTripProposalService TripProposalService { get; set; } = default!;
    [Inject] private IImportExistingTripService ExistingTripService { get; set; } = default!;
    [Inject] private IImportPersistenceService PersistenceService { get; set; } = default!;
    [Inject] private INetworkService NetworkService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private bool CanReview
    {
        get
        {
            return _batch?.IsProcessingPhotos == false
                && _batch.Photos.Any(photo => !photo.IsRemoved && photo.IsReady);
        }
    }

    private IReadOnlyList<ImportCatchProposalModel> ActiveCatchProposals =>
        [.. _batch?.CatchProposals.Where(proposal => !proposal.IsRemoved) ?? []];

    private IReadOnlyList<ImportTripProposalModel> ActiveTripProposals =>
        [.. _batch?.TripProposals.Where(proposal => !proposal.IsRemoved) ?? []];

    private IReadOnlyList<CatalogueOptionModel> MethodOptions
    {
        get
        {
            var preferredIds = _preferences.Preferences.Methods
                .OrderByDescending(method => method.IsDefault)
                .Select(method => method.FishingMethodId)
                .ToArray();
            var methods = preferredIds.Length > 0
                ? preferredIds.Select(FindMethod).Where(method => method is not null).Select(method => method!)
                : _preferences.Catalogue.Methods;
            var selected = FindMethod(_methodId);
            return BuildShortlist(methods.Select(Option), selected is null ? null : Option(selected));
        }
    }

    private IReadOnlyList<CatalogueOptionModel> SpeciesOptions
    {
        get
        {
            var preferred = _preferences.Preferences.Methods
                .FirstOrDefault(method => method.FishingMethodId == _methodId)?
                .Species
                .OrderByDescending(species => species.IsDefault)
                .Select(species => FindSpecies(species.SpeciesId))
                .Where(species => species is not null)
                .Select(species => Option(species!))
                .ToArray();
            var selected = FindSpecies(_speciesId);
            return BuildShortlist(preferred ?? [], selected is null ? null : Option(selected));
        }
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _preferences = await Preferences.GetAsync(_cancellationTokenSource.Token);
            ApplyPreferredDefaults();
            _batch = NewBatch([]);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ApplyPreferredDefaults()
    {
        var method = _preferences.Preferences.Methods.FirstOrDefault(candidate => candidate.IsDefault);
        if (method is null)
        {
            return;
        }

        _methodId = method.FishingMethodId;
        _speciesId = method.Species.FirstOrDefault(candidate => candidate.IsDefault)?.SpeciesId ?? Guid.Empty;
    }

    private async Task ChooseMethodAsync()
    {
        var chosen = await ChooseAsync(
            Loc["Import_FishingMethod"],
            _preferences.Catalogue.Methods
                .Select(method => new CatalogueOptionModel(method.Id, method.Code, method.Name))
                .ToArray());
        if (chosen is not null)
        {
            SelectMethod(chosen.Id);
        }
    }

    private async Task ChooseSpeciesAsync()
    {
        var chosen = await ChooseAsync(
            Loc["Import_Species"],
            _preferences.Catalogue.AllSpecies
                .Select(species => new CatalogueOptionModel(species.Id, species.Code, species.Name))
                .ToArray());
        if (chosen is not null)
        {
            SelectSpecies(chosen.Id);
        }
    }

    private void SelectMethod(Guid methodId)
    {
        _methodId = methodId;
        ApplyDefaults();
    }

    private void SelectSpecies(Guid speciesId)
    {
        _speciesId = speciesId;
        ApplyDefaults();
    }

    private async Task<CatalogueOptionModel?> ChooseAsync(
        string title,
        IReadOnlyList<CatalogueOptionModel> options)
    {
        var result = await ModalService.ShowAsync<
            CataloguePickerModal,
            CataloguePickerModalModel,
            CataloguePickerModalResult>(
                new CataloguePickerModalModel(title, options),
                _cancellationTokenSource.Token);
        return result?.Options.SingleOrDefault();
    }

    private void ApplyDefaults()
    {
        _batch?.SetDefaults(SelectedMethod(), SelectedSpecies());
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
        return _preferences.Catalogue.Methods.SingleOrDefault(candidate => candidate.Id == id);
    }

    private SpeciesDto? FindSpecies(Guid id)
    {
        return _preferences.Catalogue.AllSpecies.SingleOrDefault(candidate => candidate.Id == id);
    }

    private static CatalogueOptionModel Option(FishingMethodDto method)
    {
        return new CatalogueOptionModel(method.Id, method.Code, method.Name);
    }

    private static CatalogueOptionModel Option(SpeciesDto species)
    {
        return new CatalogueOptionModel(species.Id, species.Code, species.Name);
    }

    private static IReadOnlyList<CatalogueOptionModel> BuildShortlist(
        IEnumerable<CatalogueOptionModel> options,
        CatalogueOptionModel? selected)
    {
        var shortlist = options.Take(MaxChipOptions).ToList();
        if (selected is null || shortlist.Any(option => option.Id == selected.Id))
        {
            return shortlist;
        }

        shortlist.Insert(0, selected);
        return shortlist;
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

    private void ContinueToPhotos()
    {
        _batch?.SetStage(ImportStageEnum.ChoosePhotos);
    }

    private void BackToBatch()
    {
        _batch?.SetStage(ImportStageEnum.BatchDetails);
    }

    private void BackToPhotos()
    {
        _batch?.SetStage(ImportStageEnum.ChoosePhotos);
    }

    private void OnCorrectionsChanged()
    {
        StateHasChanged();
    }

    private async Task CompleteCorrections()
    {
        if (_batch?.CanAdvanceToTrips == true)
        {
            _batch.ReplaceTripProposals(TripProposalService.Propose(_batch));
            _existingTrips = await ExistingTripService.GetCandidatesAsync(
                ActiveTripProposals,
                _cancellationTokenSource.Token);
            _batch.SetStage(ImportStageEnum.ReviewTrips);
        }
    }

    private void BackToCatchReview()
    {
        _batch?.SetStage(ImportStageEnum.ReviewCatches);
    }

    private void DecideTrip(Guid proposalId, ImportTripDecisionEnum decision)
    {
        _batch?.DecideTrip(proposalId, decision);
    }

    private void ChooseExistingTrip(Guid proposalId, Guid tripId)
    {
        _batch?.DecideTrip(proposalId, ImportTripDecisionEnum.UseExisting, tripId);
    }

    private void RemoveCatchFromTrip(Guid proposalId, Guid catchId)
    {
        _batch?.RemoveCatchFromTrip(proposalId, catchId);
    }

    private ImportCatchProposalModel CatchProposal(Guid catchId)
    {
        return _batch!.CatchProposals.Single(proposal => proposal.Id == catchId && !proposal.IsRemoved);
    }

    private ImportSelectedPhotoModel CatchThumbnail(ImportCatchProposalModel proposal)
    {
        return proposal.PhotoIds
            .Select(photoId => _batch!.Photos.Single(photo => photo.Id == photoId))
            .OrderBy(photo => photo.SelectionIndex)
            .First();
    }

    private void ContinueToConfirmation()
    {
        if (_batch?.IsReadyForConfirmation == true)
        {
            _batch.SetStage(ImportStageEnum.Confirm);
        }
    }

    private int ActivePhotoCount => ActiveCatchProposals.Sum(proposal => proposal.PhotoIds.Count);

    private int NewTripCount => ActiveTripProposals.Count(
        proposal => proposal.Decision == ImportTripDecisionEnum.CreateNew);

    private int ExistingTripAssignmentCount => ActiveTripProposals.Count(
        proposal => proposal.Decision == ImportTripDecisionEnum.UseExisting);

    private int StandaloneCatchCount
    {
        get
        {
            var assigned = ActiveTripProposals.SelectMany(proposal => proposal.CatchProposalIds).ToHashSet();
            return ActiveCatchProposals.Count(proposal => !assigned.Contains(proposal.Id));
        }
    }

    private async Task PersistAsync()
    {
        if (_batch is null || _isPersisting || _isPersisted)
        {
            return;
        }

        _isPersisting = true;
        try
        {
            _persistenceError = null;
            _persistenceProgress = null;
            if (!await NetworkService.IsOnlineAsync(_cancellationTokenSource.Token))
            {
                _persistenceError = Loc["Import_OnlineRequired"];
                return;
            }

            _persistenceResult = await PersistenceService.PersistAsync(
                _batch,
                _cancellationTokenSource.Token,
                new Progress<ImportPersistenceProgressModel>(OnPersistenceProgress));
            _isPersisted = true;
            await Preparation.ClearAsync(CancellationToken.None);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            _persistenceError = Loc["Import_PersistenceFailed"];
        }
        finally
        {
            _isPersisting = false;
        }
    }

    private void OnPersistenceProgress(ImportPersistenceProgressModel progress)
    {
        _persistenceProgress = progress;
        StateHasChanged();
    }

    private string PersistenceProgressLabel()
    {
        if (_persistenceProgress is null)
        {
            return Loc["Import_Importing"];
        }

        var resource = _persistenceProgress.Stage switch
        {
            ImportPersistenceStageEnum.SavingTrip => "Import_ProgressSavingTrip",
            ImportPersistenceStageEnum.SavingCatch => "Import_ProgressSavingCatch",
            ImportPersistenceStageEnum.UploadingPhotograph => "Import_ProgressUploadingPhotograph",
            _ => "Import_ProgressVerifying"
        };
        return Loc[resource, _persistenceProgress.Current, _persistenceProgress.Total];
    }

    private void NavigateToCatches()
    {
        Navigation.NavigateTo("/catches");
    }

    private void NavigateToTrips()
    {
        Navigation.NavigateTo("/trips");
    }

    private async Task AddTripParticipantAsync(ImportTripProposalModel proposal)
    {
        var selected = await ModalService.ShowAsync<AnglerPickerModal, AnglerPickerModalModel, AnglerPickerModalResult>(
            new AnglerPickerModalModel(proposal.Participants.Select(participant => participant.UserId).ToArray())
            {
                Title = Loc["Import_AddTripParticipant"],
                ActionLabel = Loc["Import_Add"]
            },
            _cancellationTokenSource.Token);
        if (selected is not null)
        {
            _batch?.AddTripParticipant(proposal.Id, selected.Angler);
        }
    }

    private void RemoveTripParticipant(Guid proposalId, Guid userId)
    {
        _batch?.RemoveTripParticipant(proposalId, userId);
    }

    private string ParticipantName(AnglerSummaryDto participant)
    {
        return participant.DisplayName ?? participant.Email ?? Loc["Trip_ContributorUnknown"].Value;
    }

    private IReadOnlyList<TripSummaryDto> ExistingTripsFor(ImportTripProposalModel proposal)
    {
        return _existingTrips.GetValueOrDefault(proposal.Id) ?? [];
    }

    private async Task RemoveCorrectionPhotosAsync(IReadOnlyList<Guid> photoIds)
    {
        if (_batch is null)
        {
            return;
        }

        foreach (var photoId in photoIds)
        {
            var photo = _batch.Photos.Single(photo => photo.Id == photoId);
            await Preparation.RemoveAsync(photo, _cancellationTokenSource.Token);
            _batch.RemovePhoto(photoId);
        }
    }

    private async Task RemoveCorrectionCatchAsync(Guid catchProposalId)
    {
        if (_batch is null)
        {
            return;
        }

        var proposal = _batch.CatchProposals.Single(proposal => proposal.Id == catchProposalId);
        foreach (var photoId in proposal.PhotoIds.ToArray())
        {
            var photo = _batch.Photos.Single(photo => photo.Id == photoId);
            await Preparation.RemoveAsync(photo, _cancellationTokenSource.Token);
        }

        _batch.RemoveCatchProposal(catchProposalId);
    }

    private void OnSelectionStarted()
    {
        _batch?.BeginPhotoProcessing();
        _selectionLimitExceeded = false;
    }

    private void OnSelectionLimitExceeded()
    {
        _batch?.CompletePhotoProcessing();
        _selectionLimitExceeded = true;
    }

    private void OnPhotosPrepared(IReadOnlyList<ImportSelectedPhotoModel> photos)
    {
        _batch = NewBatch(photos);
        _batch.SetStage(ImportStageEnum.ChoosePhotos);
        _batch.CompletePhotoProcessing();
        _selectionLimitExceeded = false;
    }

    private ImportBatchModel NewBatch(IEnumerable<ImportSelectedPhotoModel> photos)
    {
        var batch = new ImportBatchModel(_batch?.Id ?? Guid.NewGuid(), SelectedMethod(), SelectedSpecies());
        foreach (var photo in photos)
        {
            batch.AddPhoto(photo);
        }

        return batch;
    }

    private async Task RemovePhotoAsync(ImportSelectedPhotoModel photo)
    {
        await Preparation.RemoveAsync(photo, _cancellationTokenSource.Token);
        _batch?.RemovePhoto(photo.Id);
    }

    private void ContinueToReview()
    {
        if (_batch is null || !CanReview)
        {
            return;
        }

        _batch.ReplaceCatchProposals(ProposalService.Propose(_batch));
        _batch.SetStage(ImportStageEnum.ReviewCatches);
    }

    private string PhotoPreparationLabel(ImportSelectedPhotoModel photo)
    {
        return photo.PreparationStatus switch
        {
            ImportPhotoPreparationStatusEnum.UnsupportedType => Loc["Import_PhotoUnsupported"],
            ImportPhotoPreparationStatusEnum.TooLarge => Loc["Import_PhotoTooLarge"],
            ImportPhotoPreparationStatusEnum.Cancelled => Loc["Import_PhotoCancelled"],
            _ => Loc["Import_PhotoFailed"]
        };
    }

    private string PhotoMetadataLabel(ImportSelectedPhotoModel photo)
    {
        return photo.MetadataStatus is ImportMetadataStatusEnum.Failed or ImportMetadataStatusEnum.Unavailable
            ? Loc["Import_MetadataUnavailable"]
            : Loc["Import_PhotoReady"];
    }

    public async ValueTask DisposeAsync()
    {
        await _cancellationTokenSource.CancelAsync();
        await Preparation.ClearAsync(CancellationToken.None);
        _cancellationTokenSource.Dispose();
    }
}
