using FishingLogBook.Mobile.Features.LocalPersistence.Enums;
using FishingLogBook.Mobile.Features.LocalPersistence.Models;
using FishingLogBook.Mobile.Features.LocalPersistence.Sqlite;
using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Features.LocalPersistence.Stores;

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
                lasterror = $lastError,
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
                   status, attemptcount, createdon, availableat, lastattemptat, lasterror
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
                   status, attemptcount, createdon, availableat, lastattemptat, lasterror
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
                LastError = reader.IsDBNull(11) ? null : reader.GetString(11)
            });
        }

        return results;
    }
}
