using FishingLogBook.Mobile.Features.Catch.Persistence.Models;

namespace FishingLogBook.Mobile.Features.Catch.Persistence.Services;

public interface ILocalCatchService
{
    Task<LocalCatchSaveResultModel> SaveAsync(
        LocalCatchSaveArgs args,
        CancellationToken cancellationToken);

    Task<LocalCatchRecordModel?> GetAsync(Guid catchId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LocalCatchRecordModel>> ListAsync(CancellationToken cancellationToken);
}
