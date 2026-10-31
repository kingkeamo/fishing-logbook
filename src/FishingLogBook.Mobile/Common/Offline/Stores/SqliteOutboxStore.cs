using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Models;
using FishingLogBook.Mobile.Common.Offline.Sqlite;
using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Common.Offline.Stores;

public sealed class SqliteOutboxStore : IOutboxStore
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly ISqliteDatabase _database;

    public SqliteOutboxStore(ISqliteConnectionFactory connectionFactory, ISqliteDatabase database)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(database);
        _connectionFactory = connectionFactory;
        _database = database;
    }

    public async Task UpsertPendingAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        OutboxOperationModel operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Status != OutboxStatusEnum.Pending)
        {
            throw new ArgumentException("Only pending outbox operations can be upserted.", nameof(operation));
        }

        await using var existingCommand = connection.CreateCommand();
        existingCommand.Transaction = transaction;
        existingCommand.CommandText =
            """
            SELECT id, attemptcount
            FROM outboxoperations
            WHERE userid = $userId
              AND aggregatetype = $aggregateType
              AND aggregateid = $aggregateId
              AND operationtype = $operationType
              AND status = 'Pending'
            LIMIT 1;
            """;
        existingCommand.Parameters.AddWithValue("$userId", operation.UserId.ToString("D"));
        existingCommand.Parameters.AddWithValue("$aggregateType", operation.AggregateType.ToString());
        existingCommand.Parameters.AddWithValue("$aggregateId", operation.AggregateId.ToString("D"));
        existingCommand.Parameters.AddWithValue("$operationType", operation.OperationType.ToString());

        Guid? existingId = null;
        var existingAttemptCount = 0;
        await using (var reader = await existingCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                existingId = Guid.Parse(reader.GetString(0));
                existingAttemptCount = reader.GetInt32(1);
            }
        }

        if (existingId is null)
        {
            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
                """
                INSERT INTO outboxoperations (
                    id, userid, aggregatetype, aggregateid, operationtype,
                    payloadjson, status, attemptcount, createdon, availableat,
                    lastattemptat, lasterror)
                VALUES (
                    $id, $userId, $aggregateType, $aggregateId, $operationType,
                    $payloadJson, 'Pending', $attemptCount, $createdOn, $availableAt,
                    $lastAttemptAt, $lastError);
                """;
            insertCommand.Parameters.AddWithValue("$id", operation.Id.ToString("D"));
            insertCommand.Parameters.AddWithValue("$userId", operation.UserId.ToString("D"));
            insertCommand.Parameters.AddWithValue("$aggregateType", operation.AggregateType.ToString());
            insertCommand.Parameters.AddWithValue("$aggregateId", operation.AggregateId.ToString("D"));
            insertCommand.Parameters.AddWithValue("$operationType", operation.OperationType.ToString());
            insertCommand.Parameters.AddWithValue("$payloadJson", operation.PayloadJson);
            insertCommand.Parameters.AddWithValue("$attemptCount", operation.AttemptCount);
            insertCommand.Parameters.AddWithValue("$createdOn", SqliteDateTime.Format(operation.CreatedOn));
            insertCommand.Parameters.AddWithValue("$availableAt", SqliteDateTime.Format(operation.AvailableAt));
            insertCommand.Parameters.AddWithValue(
                "$lastAttemptAt",
                operation.LastAttemptAt is null
                    ? DBNull.Value
                    : SqliteDateTime.Format(operation.LastAttemptAt.Value));
            insertCommand.Parameters.AddWithValue("$lastError", (object?)operation.LastError ?? DBNull.Value);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            return;
        }

        await using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText =
            """
            UPDATE outboxoperations
            SET payloadjson = $payloadJson,
                availableat = $availableAt,
                lastattemptat = $lastAttemptAt,
                lasterror = COALESCE($lastError, lasterror),
                attemptcount = $attemptCount
            WHERE id = $id
              AND userid = $userId
              AND status = 'Pending';
            """;
        updateCommand.Parameters.AddWithValue("$payloadJson", operation.PayloadJson);
        updateCommand.Parameters.AddWithValue("$availableAt", SqliteDateTime.Format(operation.AvailableAt));
        updateCommand.Parameters.AddWithValue(
            "$lastAttemptAt",
            operation.LastAttemptAt is null
                ? DBNull.Value
                : SqliteDateTime.Format(operation.LastAttemptAt.Value));
        updateCommand.Parameters.AddWithValue("$lastError", (object?)operation.LastError ?? DBNull.Value);
        updateCommand.Parameters.AddWithValue("$attemptCount", existingAttemptCount);
        updateCommand.Parameters.AddWithValue("$id", existingId.Value.ToString("D"));
        updateCommand.Parameters.AddWithValue("$userId", operation.UserId.ToString("D"));
        var updated = await updateCommand.ExecuteNonQueryAsync(cancellationToken);
        if (updated != 1)
        {
            throw new InvalidOperationException("Failed to coalesce the pending outbox operation.");
        }
    }

    public async Task<IReadOnlyList<OutboxOperationModel>> ListPendingAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _database.EnsureInitializedAsync(cancellationToken);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, userid, aggregatetype, aggregateid, operationtype, payloadjson,
                   status, attemptcount, createdon, availableat, lastattemptat, lasterror,
                   leaseowner, leaseexpiresat
            FROM outboxoperations
            WHERE userid = $userId
              AND status = 'Pending'
            ORDER BY createdon ASC, id ASC;
            """;
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        return await ReadAllAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxOperationModel>> ListPendingForAggregateAsync(
        Guid userId,
        OutboxAggregateTypeEnum aggregateType,
        Guid aggregateId,
        CancellationToken cancellationToken)
    {
        await _database.EnsureInitializedAsync(cancellationToken);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, userid, aggregatetype, aggregateid, operationtype, payloadjson,
                   status, attemptcount, createdon, availableat, lastattemptat, lasterror,
                   leaseowner, leaseexpiresat
            FROM outboxoperations
            WHERE userid = $userId
              AND aggregatetype = $aggregateType
              AND aggregateid = $aggregateId
              AND status = 'Pending'
            ORDER BY createdon ASC, id ASC;
            """;
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        command.Parameters.AddWithValue("$aggregateType", aggregateType.ToString());
        command.Parameters.AddWithValue("$aggregateId", aggregateId.ToString("D"));
        return await ReadAllAsync(command, cancellationToken);
    }

    public async Task<int> RecoverExpiredLeasesAsync(
        Guid userId,
        string currentOwner,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentOwner);
        var recovered = 0;
        await _database.ExecuteInTransactionAsync(
            async (connection, transaction, token) =>
            {
                recovered += await ExecuteAsync(
                    connection,
                    transaction,
                    $"""
                    UPDATE outboxoperations
                    SET status = 'Completed', leaseowner = NULL, leaseexpiresat = NULL, lasterror = 'Superseded'
                    WHERE {ExpiredLeaseSql} AND {PendingSiblingSql};
                    """,
                    token,
                    ("$userId", userId.ToString("D")),
                    ("$owner", currentOwner),
                    ("$now", SqliteDateTime.Format(now)));
                recovered += await ExecuteAsync(
                    connection,
                    transaction,
                    $"""
                    UPDATE outboxoperations
                    SET status = 'Pending', leaseowner = NULL, leaseexpiresat = NULL, lasterror = 'LeaseExpired'
                    WHERE {ExpiredLeaseSql};
                    """,
                    token,
                    ("$userId", userId.ToString("D")),
                    ("$owner", currentOwner),
                    ("$now", SqliteDateTime.Format(now)));
            },
            cancellationToken);
        return recovered;
    }

    public async Task<OutboxOperationModel?> ClaimNextAsync(
        Guid userId,
        string owner,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        OutboxOperationModel? claimed = null;
        await _database.ExecuteInTransactionAsync(
            async (connection, transaction, token) =>
            {
                await using var select = connection.CreateCommand();
                select.Transaction = transaction;
                select.CommandText =
                    """
                    SELECT id
                    FROM outboxoperations
                    WHERE userid = $userId
                      AND status = 'Pending'
                      AND availableat <= $now
                    ORDER BY CASE operationtype WHEN 'UploadCatchPhotograph' THEN 1 ELSE 0 END ASC, createdon ASC, id ASC
                    LIMIT 1;
                    """;
                select.Parameters.AddWithValue("$userId", userId.ToString("D"));
                select.Parameters.AddWithValue("$now", SqliteDateTime.Format(now));
                if (await select.ExecuteScalarAsync(token) is not string id)
                {
                    return;
                }

                var updated = await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    UPDATE outboxoperations
                    SET status = 'InProgress', leaseowner = $owner, leaseexpiresat = $expires,
                        lastattemptat = $now, attemptcount = attemptcount + 1
                    WHERE id = $id AND userid = $userId AND status = 'Pending';
                    """,
                    token,
                    ("$id", id),
                    ("$userId", userId.ToString("D")),
                    ("$owner", owner),
                    ("$expires", SqliteDateTime.Format(now.Add(leaseDuration))),
                    ("$now", SqliteDateTime.Format(now)));
                if (updated != 1)
                {
                    return;
                }

                await using var read = connection.CreateCommand();
                read.Transaction = transaction;
                read.CommandText =
                    """
                    SELECT id, userid, aggregatetype, aggregateid, operationtype, payloadjson,
                           status, attemptcount, createdon, availableat, lastattemptat, lasterror,
                           leaseowner, leaseexpiresat
                    FROM outboxoperations
                    WHERE id = $id;
                    """;
                read.Parameters.AddWithValue("$id", id);
                claimed = (await ReadAllAsync(read, token)).Single();
            },
            cancellationToken);
        return claimed;
    }

    public async Task CompleteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        OutboxOperationModel claimed,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(claimed);
        var updated = await ExecuteAsync(
            connection,
            transaction,
            """
            UPDATE outboxoperations
            SET status = 'Completed', leaseowner = NULL, leaseexpiresat = NULL,
                lasterror = NULL, lastattemptat = $now
            WHERE id = $id AND userid = $userId AND status = 'InProgress' AND leaseowner = $owner;
            """,
            cancellationToken,
            ("$id", claimed.Id.ToString("D")),
            ("$userId", claimed.UserId.ToString("D")),
            ("$owner", claimed.LeaseOwner ?? string.Empty),
            ("$now", SqliteDateTime.Format(now)));
        if (updated != 1)
        {
            throw new InvalidOperationException("The outbox operation lease was lost before completion.");
        }
    }

    public Task ReleaseAsync(OutboxOperationModel claimed, CancellationToken cancellationToken)
    {
        return FinishUnsuccessfulAsync(claimed, OutboxStatusEnum.Pending, null, null, -1, cancellationToken);
    }

    public Task ScheduleRetryAsync(
        OutboxOperationModel claimed,
        DateTimeOffset availableAt,
        string errorCode,
        CancellationToken cancellationToken)
    {
        return FinishUnsuccessfulAsync(claimed, OutboxStatusEnum.Pending, availableAt, errorCode, 0, cancellationToken);
    }

    public Task MarkTerminalAsync(
        OutboxOperationModel claimed,
        OutboxStatusEnum status,
        string errorCode,
        CancellationToken cancellationToken)
    {
        if (status is not (OutboxStatusEnum.Failed or OutboxStatusEnum.Dead))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        return FinishUnsuccessfulAsync(claimed, status, null, errorCode, 0, cancellationToken);
    }

    public async Task<int> ResetForRetryAsync(
        Guid userId,
        Guid catchId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var reset = 0;
        await _database.ExecuteInTransactionAsync(
            async (connection, transaction, token) =>
            {
                var parameters = new (string, object)[]
                {
                    ("$userId", userId.ToString("D")),
                    ("$catchId", catchId.ToString("D")),
                    ("$now", SqliteDateTime.Format(now))
                };
                reset += await ExecuteAsync(
                    connection,
                    transaction,
                    $"""
                    UPDATE outboxoperations
                    SET availableat = $now, attemptcount = 0, lasterror = NULL
                    WHERE userid = $userId AND status = 'Pending' AND {RelatedToCatchSql};
                    """,
                    token,
                    parameters);
                reset += await ExecuteAsync(
                    connection,
                    transaction,
                    $"""
                    UPDATE outboxoperations
                    SET status = 'Pending', availableat = $now, attemptcount = 0, lasterror = NULL
                    WHERE userid = $userId AND status IN ('Failed', 'Dead') AND {RelatedToCatchSql}
                      AND id = (SELECT l.id FROM outboxoperations l
                                WHERE l.userid = outboxoperations.userid
                                  AND l.aggregatetype = outboxoperations.aggregatetype
                                  AND l.aggregateid = outboxoperations.aggregateid
                                  AND l.operationtype = outboxoperations.operationtype
                                ORDER BY l.createdon DESC, l.id DESC LIMIT 1);
                    """,
                    token,
                    parameters);
            },
            cancellationToken);
        return reset;
    }

    public async Task<DateTimeOffset?> GetNextDueAsync(Guid userId, CancellationToken cancellationToken)
    {
        await _database.EnsureInitializedAsync(cancellationToken);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT MIN(availableat)
            FROM outboxoperations
            WHERE userid = $userId AND status = 'Pending';
            """;
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        return await command.ExecuteScalarAsync(cancellationToken) is string value
            ? SqliteDateTime.Parse(value)
            : null;
    }

    private const string ExpiredLeaseSql =
        """
        status = 'InProgress' AND userid = $userId
        AND (leaseowner IS NULL OR leaseowner <> $owner OR leaseexpiresat IS NULL OR leaseexpiresat <= $now)
        """;

    private const string PendingSiblingSql =
        """
        EXISTS (SELECT 1 FROM outboxoperations p
                WHERE p.userid = outboxoperations.userid
                  AND p.aggregatetype = outboxoperations.aggregatetype
                  AND p.aggregateid = outboxoperations.aggregateid
                  AND p.operationtype = outboxoperations.operationtype
                  AND p.status = 'Pending')
        """;

    private const string RelatedToCatchSql =
        """
        ((aggregatetype = 'Catch' AND aggregateid = $catchId)
         OR (aggregatetype = 'CatchPhotograph' AND aggregateid IN
              (SELECT photographid FROM localphotographsyncs WHERE catchid = $catchId AND userid = $userId)))
        """;

    private async Task FinishUnsuccessfulAsync(
        OutboxOperationModel claimed,
        OutboxStatusEnum target,
        DateTimeOffset? availableAt,
        string? errorCode,
        int attemptDelta,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claimed);
        await _database.ExecuteInTransactionAsync(
            async (connection, transaction, token) =>
            {
                var identity = new (string, object)[]
                {
                    ("$id", claimed.Id.ToString("D")),
                    ("$userId", claimed.UserId.ToString("D")),
                    ("$owner", claimed.LeaseOwner ?? string.Empty)
                };
                var superseded = await ExecuteAsync(
                    connection,
                    transaction,
                    $"""
                    UPDATE outboxoperations
                    SET status = 'Completed', leaseowner = NULL, leaseexpiresat = NULL, lasterror = 'Superseded'
                    WHERE id = $id AND userid = $userId AND status = 'InProgress' AND leaseowner = $owner
                      AND {PendingSiblingSql};
                    """,
                    token,
                    identity);
                if (superseded == 1)
                {
                    return;
                }

                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    UPDATE outboxoperations
                    SET status = $status, availableat = COALESCE($availableAt, availableat),
                        lasterror = $errorCode, attemptcount = MAX(0, attemptcount + $attemptDelta),
                        leaseowner = NULL, leaseexpiresat = NULL
                    WHERE id = $id AND userid = $userId AND status = 'InProgress' AND leaseowner = $owner;
                    """,
                    token,
                    identity.Concat(new (string, object)[]
                    {
                        ("$status", target.ToString()),
                        ("$availableAt", availableAt is null ? DBNull.Value : SqliteDateTime.Format(availableAt.Value)),
                        ("$errorCode", (object?)errorCode ?? DBNull.Value),
                        ("$attemptDelta", attemptDelta)
                    }).ToArray());
            },
            cancellationToken);
    }

    private static async Task<int> ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<OutboxOperationModel>> ReadAllAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        var results = new List<OutboxOperationModel>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new OutboxOperationModel
            {
                Id = Guid.Parse(reader.GetString(0)),
                UserId = Guid.Parse(reader.GetString(1)),
                AggregateType = Enum.Parse<OutboxAggregateTypeEnum>(reader.GetString(2)),
                AggregateId = Guid.Parse(reader.GetString(3)),
                OperationType = Enum.Parse<OutboxOperationTypeEnum>(reader.GetString(4)),
                PayloadJson = reader.GetString(5),
                Status = Enum.Parse<OutboxStatusEnum>(reader.GetString(6)),
                AttemptCount = reader.GetInt32(7),
                CreatedOn = SqliteDateTime.Parse(reader.GetString(8)),
                AvailableAt = SqliteDateTime.Parse(reader.GetString(9)),
                LastAttemptAt = reader.IsDBNull(10) ? null : SqliteDateTime.Parse(reader.GetString(10)),
                LastError = reader.IsDBNull(11) ? null : reader.GetString(11),
                LeaseOwner = reader.IsDBNull(12) ? null : reader.GetString(12),
                LeaseExpiresAt = reader.IsDBNull(13) ? null : SqliteDateTime.Parse(reader.GetString(13))
            });
        }

        return results;
    }
}
