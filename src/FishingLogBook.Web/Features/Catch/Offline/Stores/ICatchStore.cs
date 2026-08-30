using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Features.Catch.Offline.Stores;

public interface ICatchStore
{
    Task SaveAsync(CatchModel catchRecord, CancellationToken cancellationToken);

    Task<IReadOnlyList<CatchModel>> GetAllAsync(Guid ownerUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CatchModel>> GetMetadataAsync(Guid ownerUserId, CancellationToken cancellationToken);

    Task<CatchModel?> GetMetadataAsync(
        Guid ownerUserId,
        Guid catchId,
        CancellationToken cancellationToken);

    Task<CatchModel?> GetAsync(
        Guid ownerUserId,
        Guid catchId,
        CancellationToken cancellationToken);

    Task UpdateSyncStateAsync(CatchModel catchRecord, CancellationToken cancellationToken);

    // Unlike UpdateSyncStateAsync, this also overwrites identity/detail fields from a
    // confirmed server-persisted record - use only once a sync response is known to match
    // what was sent (no concurrent local edit raced the request).
    Task ReconcileMetadataAsync(CatchModel catchRecord, CancellationToken cancellationToken);

    Task UpdateTripAsync(
        Guid ownerUserId,
        Guid catchId,
        Guid? tripId,
        CancellationToken cancellationToken);

    Task<byte[]?> GetPhotographBytesAsync(
        Guid ownerUserId,
        Guid catchId,
        Guid photographId,
        CancellationToken cancellationToken);

    Task<int> CleanupSyncedCacheAsync(
        Guid ownerUserId,
        DateTimeOffset olderThan,
        CancellationToken cancellationToken);
}
