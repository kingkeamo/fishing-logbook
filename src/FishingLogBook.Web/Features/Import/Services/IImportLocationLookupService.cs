using FishingLogBook.Web.Features.Import.Models;

namespace FishingLogBook.Web.Features.Import.Services;

public interface IImportLocationLookupService
{
    Task ResolveAsync(
        IReadOnlyList<ImportSelectedPhotoModel> photos,
        CancellationToken cancellationToken);
}
