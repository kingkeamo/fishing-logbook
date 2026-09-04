using FishingLogBook.Web.Features.Import.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace FishingLogBook.Web.Features.Import.Services;

public interface IImportPhotoPreparationService
{
    Task<IReadOnlyList<ImportSelectedPhotoModel>> PrepareSelectionAsync(
        IReadOnlyList<IBrowserFile> files,
        CancellationToken cancellationToken);

    Task RemoveAsync(ImportSelectedPhotoModel photo, CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
