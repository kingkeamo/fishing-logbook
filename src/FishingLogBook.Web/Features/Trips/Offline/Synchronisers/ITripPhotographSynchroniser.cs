namespace FishingLogBook.Web.Features.Trips.Offline.Synchronisers;

public interface ITripPhotographSynchroniser
{
    Task SynchronisePendingAsync(Guid ownerUserId, CancellationToken cancellationToken);
}
