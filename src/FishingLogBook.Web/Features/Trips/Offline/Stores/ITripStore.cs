using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Features.Trips.Offline.Stores;

public interface ITripStore
{
    Task SaveAsync(TripModel trip, CancellationToken cancellationToken);

    Task<IReadOnlyList<TripModel>> GetAllAsync(Guid ownerUserId, CancellationToken cancellationToken);

    Task<TripModel?> GetAsync(Guid ownerUserId, Guid tripId, CancellationToken cancellationToken);

    Task<TripModel?> GetActiveAsync(Guid ownerUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TripModel>> GetPendingAsync(Guid ownerUserId, CancellationToken cancellationToken);

    Task<int> CleanupSyncedAsync(
        Guid ownerUserId,
        DateTimeOffset olderThan,
        IReadOnlyCollection<Guid> retainedTripIds,
        CancellationToken cancellationToken);
}
