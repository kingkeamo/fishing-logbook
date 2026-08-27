namespace FishingLogBook.Web.Common.Offline.Synchronisers;

public interface ILogbookSynchroniser
{
    event EventHandler? StateChanged;

    Task SynchronisePendingAsync(CancellationToken cancellationToken);

    Task SynchronisePendingAsync(Guid ownerUserId, CancellationToken cancellationToken);

    Task CleanupSyncedCacheAsync(CancellationToken cancellationToken);

    Task CleanupSyncedCacheAsync(Guid ownerUserId, CancellationToken cancellationToken);
}
