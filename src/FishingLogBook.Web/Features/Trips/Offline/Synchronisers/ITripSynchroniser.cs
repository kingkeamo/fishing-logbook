namespace FishingLogBook.Web.Features.Trips.Offline.Synchronisers;

public interface ITripSynchroniser
{
    Task SynchronisePendingAsync(Guid ownerUserId, CancellationToken cancellationToken);

    Task CleanupSyncedCacheAsync(Guid ownerUserId, CancellationToken cancellationToken);
}
