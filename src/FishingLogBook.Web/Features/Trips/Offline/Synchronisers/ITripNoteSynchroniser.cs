namespace FishingLogBook.Web.Features.Trips.Offline.Synchronisers;

public interface ITripNoteSynchroniser
{
    Task SynchronisePendingAsync(Guid ownerUserId, CancellationToken cancellationToken);
}
