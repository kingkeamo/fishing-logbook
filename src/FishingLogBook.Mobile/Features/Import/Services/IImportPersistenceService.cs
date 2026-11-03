using FishingLogBook.Mobile.Features.Import.Models;

namespace FishingLogBook.Mobile.Features.Import.Services;

public interface IImportPersistenceService
{
    Task<ImportPersistenceResultModel> PersistAsync(
        ImportBatchModel batch,
        CancellationToken cancellationToken,
        IProgress<ImportPersistenceProgressModel>? progress = null);
}
