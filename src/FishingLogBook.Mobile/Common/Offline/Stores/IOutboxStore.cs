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
}
