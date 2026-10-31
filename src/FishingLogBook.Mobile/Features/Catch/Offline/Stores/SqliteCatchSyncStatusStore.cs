using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Sqlite;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Features.Catch.Offline.Stores;

public sealed class SqliteCatchSyncStatusStore : ICatchSyncStatusStore
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly ISqliteDatabase _database;

    public SqliteCatchSyncStatusStore(ISqliteConnectionFactory connectionFactory, ISqliteDatabase database)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(database);
        _connectionFactory = connectionFactory;
        _database = database;
    }

    public async Task<IReadOnlyDictionary<Guid, CatchSyncStatusModel>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _database.EnsureInitializedAsync(cancellationToken);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        var catches = await ReadCatchesAsync(connection, userId, cancellationToken);
        var photographs = await ReadPhotographsAsync(connection, userId, cancellationToken);
        var operations = await ReadLatestOperationsAsync(connection, userId, cancellationToken);

        var statuses = new Dictionary<Guid, CatchSyncStatusModel>(catches.Count);
        foreach (var catchRow in catches)
        {
            statuses[catchRow.Id] = BuildStatus(
                catchRow,
                photographs.Where(photograph => photograph.CatchId == catchRow.Id).ToArray(),
                operations);
        }

        return statuses;
    }

    private static CatchSyncStatusModel BuildStatus(
        CatchRow catchRow,
        IReadOnlyList<PhotographRow> photographs,
        IReadOnlyList<OperationRow> operations)
    {
        var metadataSynced = catchRow.SyncedRevision >= catchRow.Revision;
        var states = new List<CatchSyncStateEnum>();
        var attempts = new List<DateTimeOffset>();
        if (!metadataSynced)
        {
            var operation = operations.FirstOrDefault(item =>
                item.AggregateType == OutboxAggregateTypeEnum.Catch && item.AggregateId == catchRow.Id);
            states.Add(StateOf(operation));
            AddRetryTime(operation, attempts);
        }

        foreach (var photograph in photographs.Where(item => !item.Recorded))
        {
            var operation = operations.FirstOrDefault(item =>
                item.AggregateType == OutboxAggregateTypeEnum.CatchPhotograph && item.AggregateId == photograph.Id);
            states.Add(metadataSynced || operation is not null ? StateOf(operation) : CatchSyncStateEnum.WaitingToSync);
            AddRetryTime(operation, attempts);
        }

        var overall = states.Count == 0 ? CatchSyncStateEnum.Synced : states.MaxBy(Rank);
        return new CatchSyncStatusModel
        {
            CatchId = catchRow.Id,
            State = overall,
            MetadataSynced = metadataSynced,
            PhotographCount = photographs.Count,
            PhotographsRecorded = photographs.Count(item => item.Recorded),
            CanRetry = overall is CatchSyncStateEnum.NeedsAttention or CatchSyncStateEnum.ActionRequired,
            NextAttemptAt = attempts.Count == 0 ? null : attempts.Min()
        };
    }

    private static CatchSyncStateEnum StateOf(OperationRow? operation)
    {
        return operation switch
        {
            null => CatchSyncStateEnum.WaitingToSync,
            { Status: OutboxStatusEnum.InProgress } => CatchSyncStateEnum.Synchronising,
            { Status: OutboxStatusEnum.Failed } => CatchSyncStateEnum.ActionRequired,
            { Status: OutboxStatusEnum.Dead } => CatchSyncStateEnum.NeedsAttention,
            { Status: OutboxStatusEnum.Pending, AttemptCount: > 0 } => CatchSyncStateEnum.NeedsAttention,
            _ => CatchSyncStateEnum.WaitingToSync
        };
    }

    private static void AddRetryTime(OperationRow? operation, List<DateTimeOffset> attempts)
    {
        if (operation is { Status: OutboxStatusEnum.Pending, AttemptCount: > 0 })
        {
            attempts.Add(operation.AvailableAt);
        }
    }

    private static int Rank(CatchSyncStateEnum state)
    {
        return state switch
        {
            CatchSyncStateEnum.Synced => 0,
            CatchSyncStateEnum.WaitingToSync => 1,
            CatchSyncStateEnum.Synchronising => 2,
            CatchSyncStateEnum.NeedsAttention => 3,
            _ => 4
        };
    }

    private static async Task<IReadOnlyList<CatchRow>> ReadCatchesAsync(
        SqliteConnection connection,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT c.id, c.revision, COALESCE(s.syncedrevision, 0)
            FROM localcatches c
            LEFT JOIN localcatchsyncs s ON s.catchid = c.id AND s.userid = c.userid
            WHERE c.userid = $userId AND c.lifecycle = 'Saved';
            """;
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        var rows = new List<CatchRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CatchRow(Guid.Parse(reader.GetString(0)), reader.GetInt64(1), reader.GetInt64(2)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<PhotographRow>> ReadPhotographsAsync(
        SqliteConnection connection,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT p.id, p.catchid, COALESCE(s.status = 'Recorded', 0)
            FROM localcatchphotographs p
            LEFT JOIN localphotographsyncs s ON s.photographid = p.id AND s.userid = p.userid
            WHERE p.userid = $userId;
            """;
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        var rows = new List<PhotographRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new PhotographRow(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                reader.GetInt64(2) == 1));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<OperationRow>> ReadLatestOperationsAsync(
        SqliteConnection connection,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT o.aggregatetype, o.aggregateid, o.status, o.attemptcount, o.availableat
            FROM outboxoperations o
            WHERE o.userid = $userId
              AND o.operationtype IN ('UpsertCatchMetadata', 'UploadCatchPhotograph')
              AND o.id = (SELECT l.id FROM outboxoperations l
                          WHERE l.userid = o.userid
                            AND l.aggregatetype = o.aggregatetype
                            AND l.aggregateid = o.aggregateid
                            AND l.operationtype = o.operationtype
                          ORDER BY l.createdon DESC, l.id DESC LIMIT 1)
              AND o.status <> 'Completed';
            """;
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        var rows = new List<OperationRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new OperationRow(
                Enum.Parse<OutboxAggregateTypeEnum>(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                Enum.Parse<OutboxStatusEnum>(reader.GetString(2)),
                reader.GetInt32(3),
                SqliteDateTime.Parse(reader.GetString(4))));
        }

        return rows;
    }

    private sealed record CatchRow(Guid Id, long Revision, long SyncedRevision);

    private sealed record PhotographRow(Guid Id, Guid CatchId, bool Recorded);

    private sealed record OperationRow(
        OutboxAggregateTypeEnum AggregateType,
        Guid AggregateId,
        OutboxStatusEnum Status,
        int AttemptCount,
        DateTimeOffset AvailableAt);
}
