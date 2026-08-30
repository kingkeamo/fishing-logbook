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

    // Revokes this viewer's cached participant access to a server-origin shared Trip once an
    // authoritative online check confirms they are no longer an owner or accepted participant.
    // Never removes a locally-created Trip or its data - only strips the viewer from
    // ParticipantUserIds so it stops presenting as writable to them. Returns true if access was
    // actually revoked.
    Task<bool> RevokeParticipantAccessAsync(
        Guid viewerUserId,
        Guid tripId,
        CancellationToken cancellationToken);
}
