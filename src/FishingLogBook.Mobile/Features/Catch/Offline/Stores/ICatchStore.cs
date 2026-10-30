using FishingLogBook.Mobile.Features.Catch.Offline.Models;

namespace FishingLogBook.Mobile.Features.Catch.Offline.Stores;

public interface ICatchStore
{
    Task<LocalCatchSaveResultModel> SaveAsync(
        LocalCatchSaveArgs args,
        CancellationToken cancellationToken);

    Task<LocalCatchRecordModel?> GetAsync(Guid catchId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LocalCatchRecordModel>> ListAsync(CancellationToken cancellationToken);
}
