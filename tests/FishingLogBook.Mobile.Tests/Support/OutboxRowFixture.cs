using System.Globalization;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Sqlite;
using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Tests.Support;

/// <summary>Direct SQL access to outbox rows so tests can seed and inspect exact persisted state.</summary>
public static class OutboxRowFixture
{
    public static readonly DateTimeOffset Now = DateTimeOffset.Parse(
        "2026-06-01T12:00:00Z",
        CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind);

    public static async Task<Guid> InsertAsync(
        ISqliteConnectionFactory factory,
        Guid userId,
        Guid aggregateId,
        OutboxStatusEnum status = OutboxStatusEnum.Pending,
        OutboxOperationTypeEnum operationType = OutboxOperationTypeEnum.UpsertCatchMetadata,
        OutboxAggregateTypeEnum aggregateType = OutboxAggregateTypeEnum.Catch,
        int attemptCount = 0,
        DateTimeOffset? createdOn = null,
        DateTimeOffset? availableAt = null,
        string? leaseOwner = null,
        DateTimeOffset? leaseExpiresAt = null,
        Guid? id = null)
    {
        var operationId = id ?? Guid.NewGuid();
        await using var connection = await factory.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO outboxoperations (
                id, userid, aggregatetype, aggregateid, operationtype, payloadjson, status,
                attemptcount, createdon, availableat, lastattemptat, lasterror, leaseowner, leaseexpiresat)
            VALUES (
                $id, $userId, $aggregateType, $aggregateId, $operationType, '{}', $status,
                $attemptCount, $createdOn, $availableAt, NULL, NULL, $leaseOwner, $leaseExpiresAt);
            """;
        command.Parameters.AddWithValue("$id", operationId.ToString("D"));
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        command.Parameters.AddWithValue("$aggregateType", aggregateType.ToString());
        command.Parameters.AddWithValue("$aggregateId", aggregateId.ToString("D"));
        command.Parameters.AddWithValue("$operationType", operationType.ToString());
        command.Parameters.AddWithValue("$status", status.ToString());
        command.Parameters.AddWithValue("$attemptCount", attemptCount);
        command.Parameters.AddWithValue("$createdOn", Format(createdOn ?? Now.AddMinutes(-30)));
        command.Parameters.AddWithValue("$availableAt", Format(availableAt ?? Now.AddMinutes(-30)));
        command.Parameters.AddWithValue("$leaseOwner", (object?)leaseOwner ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$leaseExpiresAt",
            leaseExpiresAt is null ? DBNull.Value : Format(leaseExpiresAt.Value));
        await command.ExecuteNonQueryAsync();
        return operationId;
    }

    public static async Task<OutboxRowSnapshot> GetAsync(ISqliteConnectionFactory factory, Guid id)
    {
        await using var connection = await factory.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT status, attemptcount, availableat, lastattemptat, lasterror, leaseowner, leaseexpiresat
            FROM outboxoperations WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Outbox row not found.");
        }

        return new OutboxRowSnapshot(
            Enum.Parse<OutboxStatusEnum>(reader.GetString(0)),
            reader.GetInt32(1),
            Parse(reader.GetString(2)),
            reader.IsDBNull(3) ? null : Parse(reader.GetString(3)),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : Parse(reader.GetString(6)));
    }

    public static async Task InsertPhotographSyncAsync(
        ISqliteConnectionFactory factory,
        Guid userId,
        Guid catchId,
        Guid photographId)
    {
        await using var connection = await factory.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO localphotographsyncs (photographid, catchid, userid, status, objectkey, updatedon)
            VALUES ($photographId, $catchId, $userId, 'Pending', NULL, $now);
            """;
        command.Parameters.AddWithValue("$photographId", photographId.ToString("D"));
        command.Parameters.AddWithValue("$catchId", catchId.ToString("D"));
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        command.Parameters.AddWithValue("$now", Format(Now));
        await command.ExecuteNonQueryAsync();
    }

    public static string Format(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset Parse(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
}

public sealed record OutboxRowSnapshot(
    OutboxStatusEnum Status,
    int AttemptCount,
    DateTimeOffset AvailableAt,
    DateTimeOffset? LastAttemptAt,
    string? LastError,
    string? LeaseOwner,
    DateTimeOffset? LeaseExpiresAt);
