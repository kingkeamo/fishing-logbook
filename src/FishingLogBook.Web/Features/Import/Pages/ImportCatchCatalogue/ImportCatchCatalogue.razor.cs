using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common.Modals;
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

    [Inject] private IAnglerPreferencesProvider Preferences { get; set; } = default!;
    [Inject] private IModalService ModalService { get; set; } = default!;
    [Inject] private IImportCatchProposalService ProposalService { get; set; } = default!;
    [Inject] private IImportPhotoPreparationService Preparation { get; set; } = default!;
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private bool CanReview
    {
        get
        {
            return _batch?.IsProcessingPhotos == false
                && _batch.Photos.Any(photo => !photo.IsRemoved && photo.IsReady);
        }
    }

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
