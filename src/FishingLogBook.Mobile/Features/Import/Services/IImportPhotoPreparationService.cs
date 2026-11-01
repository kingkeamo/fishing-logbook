using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Photographs.Models;

namespace FishingLogBook.Mobile.Features.Import.Services;

public interface IImportPhotoPreparationService
{
    Task<IReadOnlyList<ImportSelectedPhotoModel>> PrepareSelectionAsync(
        IReadOnlyList<PhotographPickedFileModel> files,
        CancellationToken cancellationToken);

    Task RemoveAsync(ImportSelectedPhotoModel photo, CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
