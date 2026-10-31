using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Features.Catch.Offline.Stores;

public interface ICatchSyncStateStore
{
    Task<long> GetSyncedRevisionAsync(Guid userId, Guid catchId, CancellationToken cancellationToken);

    Task RecordSyncedRevisionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid userId,
        Guid catchId,
        long revision,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task ClearSyncedRevisionAsync(Guid userId, Guid catchId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PhotographSyncStateModel>> ListPhotographStatesAsync(
        Guid userId,
        Guid catchId,
        CancellationToken cancellationToken);

    Task<PhotographSyncStateModel?> GetPhotographStateAsync(
        Guid userId,
        Guid photographId,
        CancellationToken cancellationToken);

    Task EnsurePhotographPendingAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid userId,
        Guid catchId,
        Guid photographId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task SetPhotographStateAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        PhotographSyncStateModel state,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task SetPhotographUploadedAsync(
        PhotographSyncStateModel state,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task DeletePhotographStateAsync(Guid userId, Guid photographId, CancellationToken cancellationToken);
}
