using FishingLogBook.Mobile.Features.Import.Models;

namespace FishingLogBook.Mobile.Features.Import.Services;

public interface IImportLocationLookupService
{
    Task ResolveAsync(
        IReadOnlyList<ImportSelectedPhotoModel> photos,
        CancellationToken cancellationToken);
}
