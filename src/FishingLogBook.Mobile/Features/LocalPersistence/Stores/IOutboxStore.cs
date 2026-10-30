using FishingLogBook.Mobile.Features.LocalPersistence.Enums;
using FishingLogBook.Mobile.Features.LocalPersistence.Models;
using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Features.LocalPersistence.Stores;

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
