using FishingLogBook.Mobile.Features.Catch.Offline.Models;

namespace FishingLogBook.Mobile.Features.Catch.Offline.Stores;

public interface ICatchSyncStatusStore
{
    Task<IReadOnlyDictionary<Guid, CatchSyncStatusModel>> ListAsync(Guid userId, CancellationToken cancellationToken);
}
