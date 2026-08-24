namespace FishingLogBook.Web.Features.Catch.Offline.Synchronisers;

public interface ICatchSynchroniser
{
    event EventHandler? StateChanged;

    Task SynchronisePendingAsync(CancellationToken cancellationToken);

    Task SynchronisePendingAsync(Guid ownerUserId, CancellationToken cancellationToken);

    Task RetryAsync(Guid catchId, CancellationToken cancellationToken);
}
