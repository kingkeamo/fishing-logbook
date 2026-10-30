using FishingLogBook.Mobile.Features.Catch.Persistence.Models;
using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Features.Catch.Persistence.Stores;

public interface ILocalCatchStore
{
    Task UpsertTripStubAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        LocalTripReferenceModel trip,
        CancellationToken cancellationToken);

    Task<LocalCatchRevisionStateModel?> GetRevisionStateAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid userId,
        Guid catchId,
        CancellationToken cancellationToken);

    Task UpsertCatchAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        LocalCatchRecordModel catchRecord,
        CancellationToken cancellationToken);

    Task ReplacePhotographsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid userId,
        Guid catchId,
        IReadOnlyList<LocalCatchPhotographModel> photographs,
        CancellationToken cancellationToken);

    Task<LocalCatchRecordModel?> GetAsync(
        Guid userId,
        Guid catchId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LocalCatchRecordModel>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
