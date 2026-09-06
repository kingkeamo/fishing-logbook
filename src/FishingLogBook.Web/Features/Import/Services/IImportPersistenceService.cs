using FishingLogBook.Web.Features.Import.Models;

namespace FishingLogBook.Web.Features.Import.Services;

public interface IImportPersistenceService
{
    Task<ImportPersistenceResultModel> PersistAsync(
        ImportBatchModel batch,
        CancellationToken cancellationToken,
        IProgress<ImportPersistenceProgressModel>? progress = null);
}
