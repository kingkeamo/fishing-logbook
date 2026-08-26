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

    Task<int> CleanupSyncedCacheAsync(
        Guid ownerUserId,
        DateTimeOffset olderThan,
        CancellationToken cancellationToken);
}
