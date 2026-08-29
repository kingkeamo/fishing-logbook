using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Features.Trips.Offline.Stores;

public interface ITripStore
{
    Task SaveAsync(TripModel trip, CancellationToken cancellationToken);

    Task HydrateAsync(TripModel trip, Guid viewerUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TripModel>> GetAllAsync(Guid viewerUserId, CancellationToken cancellationToken);

    Task<TripModel?> GetAsync(Guid viewerUserId, Guid tripId, CancellationToken cancellationToken);

    Task<TripModel?> GetActiveAsync(Guid viewerUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TripModel>> GetPendingAsync(Guid ownerUserId, CancellationToken cancellationToken);

    Task<int> CleanupSyncedAsync(
        Guid viewerUserId,
        DateTimeOffset olderThan,
        IReadOnlyCollection<Guid> retainedTripIds,
        CancellationToken cancellationToken);
}
