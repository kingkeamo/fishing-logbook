using FishingLogBook.Mobile.Common.Offline.Sqlite;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Features.Catch.Offline.Stores;

public sealed class SqliteCatchSyncStateStore : ICatchSyncStateStore
{
    private const string SelectSql =
        "SELECT photographid, catchid, userid, status, objectkey FROM localphotographsyncs";

    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly ISqliteDatabase _database;

    public SqliteCatchSyncStateStore(ISqliteConnectionFactory connectionFactory, ISqliteDatabase database)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(database);
        _connectionFactory = connectionFactory;
        _database = database;
    }

    public async Task<long> GetSyncedRevisionAsync(Guid userId, Guid catchId, CancellationToken cancellationToken)
    {
        await _database.EnsureInitializedAsync(cancellationToken);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT syncedrevision FROM localcatchsyncs WHERE catchid = $catchId AND userid = $userId;";
        command.Parameters.AddWithValue("$catchId", catchId.ToString("D"));
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        return await command.ExecuteScalarAsync(cancellationToken) is long revision ? revision : 0L;
    }

    public async Task RecordSyncedRevisionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid userId,
        Guid catchId,
        long revision,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO localcatchsyncs (catchid, userid, syncedrevision, syncedon)
            VALUES ($catchId, $userId, $revision, $now)
            ON CONFLICT(catchid) DO UPDATE SET
                syncedrevision = MAX(localcatchsyncs.syncedrevision, excluded.syncedrevision),
                syncedon = excluded.syncedon
            WHERE localcatchsyncs.userid = excluded.userid;
            """;
        command.Parameters.AddWithValue("$catchId", catchId.ToString("D"));
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        command.Parameters.AddWithValue("$revision", revision);
        command.Parameters.AddWithValue("$now", SqliteDateTime.Format(now));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task ClearSyncedRevisionAsync(Guid userId, Guid catchId, CancellationToken cancellationToken)
    {
        return _database.ExecuteInTransactionAsync(
            async (connection, transaction, token) =>
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM localcatchsyncs WHERE catchid = $catchId AND userid = $userId;";
                command.Parameters.AddWithValue("$catchId", catchId.ToString("D"));
                command.Parameters.AddWithValue("$userId", userId.ToString("D"));
                await command.ExecuteNonQueryAsync(token);
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<PhotographSyncStateModel>> ListPhotographStatesAsync(
        Guid userId,
        Guid catchId,
        CancellationToken cancellationToken)
    {
        await _database.EnsureInitializedAsync(cancellationToken);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectSql + " WHERE catchid = $catchId AND userid = $userId ORDER BY photographid;";
        command.Parameters.AddWithValue("$catchId", catchId.ToString("D"));
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        return await ReadAsync(command, cancellationToken);
    }

    public async Task<PhotographSyncStateModel?> GetPhotographStateAsync(
        Guid userId,
        Guid photographId,
        CancellationToken cancellationToken)
    {
        await _database.EnsureInitializedAsync(cancellationToken);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectSql + " WHERE photographid = $photographId AND userid = $userId;";
        command.Parameters.AddWithValue("$photographId", photographId.ToString("D"));
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        return (await ReadAsync(command, cancellationToken)).SingleOrDefault();
    }

    public async Task EnsurePhotographPendingAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid userId,
        Guid catchId,
        Guid photographId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO localphotographsyncs (photographid, catchid, userid, status, objectkey, updatedon)
            VALUES ($photographId, $catchId, $userId, 'Pending', NULL, $now)
            ON CONFLICT(photographid) DO NOTHING;
            """;
        command.Parameters.AddWithValue("$photographId", photographId.ToString("D"));
        command.Parameters.AddWithValue("$catchId", catchId.ToString("D"));
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        command.Parameters.AddWithValue("$now", SqliteDateTime.Format(now));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetPhotographStateAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        PhotographSyncStateModel state,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO localphotographsyncs (photographid, catchid, userid, status, objectkey, updatedon)
            VALUES ($photographId, $catchId, $userId, $status, $objectKey, $now)
            ON CONFLICT(photographid) DO UPDATE SET
                status = excluded.status,
                objectkey = excluded.objectkey,
                updatedon = excluded.updatedon
            WHERE localphotographsyncs.userid = excluded.userid;
            """;
        command.Parameters.AddWithValue("$photographId", state.PhotographId.ToString("D"));
        command.Parameters.AddWithValue("$catchId", state.CatchId.ToString("D"));
        command.Parameters.AddWithValue("$userId", state.UserId.ToString("D"));
        command.Parameters.AddWithValue("$status", state.Status.ToString());
        command.Parameters.AddWithValue("$objectKey", (object?)state.ObjectKey ?? DBNull.Value);
        command.Parameters.AddWithValue("$now", SqliteDateTime.Format(now));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task SetPhotographUploadedAsync(
        PhotographSyncStateModel state,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return _database.ExecuteInTransactionAsync(
            (connection, transaction, token) => SetPhotographStateAsync(connection, transaction, state, now, token),
            cancellationToken);
    }

    public Task DeletePhotographStateAsync(Guid userId, Guid photographId, CancellationToken cancellationToken)
    {
        return _database.ExecuteInTransactionAsync(
            async (connection, transaction, token) =>
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM localphotographsyncs WHERE photographid = $photographId AND userid = $userId;";
                command.Parameters.AddWithValue("$photographId", photographId.ToString("D"));
                command.Parameters.AddWithValue("$userId", userId.ToString("D"));
                await command.ExecuteNonQueryAsync(token);
            },
            cancellationToken);
    }

    private static async Task<IReadOnlyList<PhotographSyncStateModel>> ReadAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        var results = new List<PhotographSyncStateModel>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new PhotographSyncStateModel
            {
                PhotographId = Guid.Parse(reader.GetString(0)),
                CatchId = Guid.Parse(reader.GetString(1)),
                UserId = Guid.Parse(reader.GetString(2)),
                Status = Enum.Parse<PhotographSyncStatusEnum>(reader.GetString(3)),
                ObjectKey = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }

        return results;
    }
}
