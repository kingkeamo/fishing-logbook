using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Models;
using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Common.Offline.Stores;

public interface IOutboxStore
{
    Task UpsertPendingAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        OutboxOperationModel operation,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OutboxOperationModel>> ListPendingAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OutboxOperationModel>> ListPendingForAggregateAsync(
        Guid userId,
        OutboxAggregateTypeEnum aggregateType,
        Guid aggregateId,
        CancellationToken cancellationToken);

    Task<int> RecoverExpiredLeasesAsync(
        Guid userId,
        string currentOwner,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<OutboxOperationModel?> ClaimNextAsync(
        Guid userId,
        string owner,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task CompleteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        OutboxOperationModel claimed,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task ReleaseAsync(OutboxOperationModel claimed, CancellationToken cancellationToken);

    Task ScheduleRetryAsync(
        OutboxOperationModel claimed,
        DateTimeOffset availableAt,
        string errorCode,
        CancellationToken cancellationToken);

    Task MarkTerminalAsync(
        OutboxOperationModel claimed,
        OutboxStatusEnum status,
        string errorCode,
        CancellationToken cancellationToken);

    Task<int> ResetForRetryAsync(
        Guid userId,
        Guid catchId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<DateTimeOffset?> GetNextDueAsync(Guid userId, CancellationToken cancellationToken);
}
