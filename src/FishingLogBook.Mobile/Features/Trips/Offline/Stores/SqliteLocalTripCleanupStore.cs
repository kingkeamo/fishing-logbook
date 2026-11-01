using FishingLogBook.Mobile.Common.Offline.Sqlite;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Constants;
using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Features.Trips.Offline.Stores;

public sealed class SqliteLocalTripCleanupStore : ILocalTripCleanupStore
{
    private const string EligibleTripSql =
        """
        d.userid = $userId
        AND d.syncedrevision >= d.revision
        AND json_extract(d.payloadjson, '$.detail.trip.status') IN ($completed, $cancelled)
        AND NOT EXISTS (SELECT 1 FROM localtripcontributions c
                        WHERE c.userid = d.userid AND c.tripid = d.tripid AND c.revision > c.syncedrevision)
        AND NOT EXISTS (SELECT 1 FROM localcatches catch
                        WHERE catch.userid = d.userid AND catch.tripid = d.tripid)
        AND NOT EXISTS (SELECT 1 FROM outboxoperations o
                        WHERE o.userid = d.userid AND o.status <> 'Completed'
                          AND ((o.aggregatetype = 'Trip' AND o.aggregateid = d.tripid)
                               OR (o.aggregatetype IN ('TripNote', 'TripPhotograph') AND o.aggregateid IN
                                   (SELECT c.id FROM localtripcontributions c
                                    WHERE c.userid = d.userid AND c.tripid = d.tripid))))
        """;

    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly ISqliteDatabase _database;

    public SqliteLocalTripCleanupStore(ISqliteConnectionFactory connectionFactory, ISqliteDatabase database)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(database);
        _connectionFactory = connectionFactory;
        _database = database;
    }

    public async Task<IReadOnlyList<LocalTripCleanupCandidateModel>> ListCandidatesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _database.EnsureInitializedAsync(cancellationToken);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        var trips = new List<(Guid Id, long Revision)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"SELECT d.tripid, d.revision FROM localtripdocuments d WHERE {EligibleTripSql} ORDER BY d.tripid;";
            BindEligibility(command, userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                trips.Add((Guid.Parse(reader.GetString(0)), reader.GetInt64(1)));
            }
        }

        var candidates = new List<LocalTripCleanupCandidateModel>(trips.Count);
        foreach (var (id, revision) in trips)
        {
            candidates.Add(await ReadCandidateAsync(connection, null, userId, id, revision, cancellationToken));
        }

        return candidates;
    }

    public async Task<bool> TryDeleteAsync(
        LocalTripCleanupCandidateModel candidate,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var deleted = false;
        await _database.ExecuteInTransactionAsync(
            async (connection, transaction, token) =>
            {
                await using var check = connection.CreateCommand();
                check.Transaction = transaction;
                check.CommandText =
                    $"SELECT COUNT(*) FROM localtripdocuments d WHERE d.tripid = $tripId AND d.revision = $revision AND {EligibleTripSql};";
                check.Parameters.AddWithValue("$tripId", candidate.TripId.ToString("D"));
                check.Parameters.AddWithValue("$revision", candidate.Revision);
                BindEligibility(check, candidate.UserId);
                if (Convert.ToInt32(await check.ExecuteScalarAsync(token)) != 1)
                {
                    return;
                }

                var current = await ReadCandidateAsync(connection, transaction, candidate.UserId, candidate.TripId, candidate.Revision, token);
                foreach (var relativePath in current.RelativeFilePaths)
                {
                    await ExecuteAsync(
                        connection,
                        transaction,
                        "INSERT INTO localpendingfiledeletions (id, userid, relativepath, createdon) VALUES ($id, $userId, $path, $now);",
                        token,
                        ("$id", Guid.NewGuid().ToString("D")),
                        ("$userId", candidate.UserId.ToString("D")),
                        ("$path", relativePath),
                        ("$now", SqliteDateTime.Format(now)));
                }

                var identity = new (string, object)[]
                {
                    ("$tripId", candidate.TripId.ToString("D")),
                    ("$userId", candidate.UserId.ToString("D"))
                };
                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    DELETE FROM outboxoperations
                    WHERE userid = $userId AND status = 'Completed'
                      AND ((aggregatetype = 'Trip' AND aggregateid = $tripId)
                           OR (aggregatetype IN ('TripNote', 'TripPhotograph') AND aggregateid IN
                               (SELECT id FROM localtripcontributions WHERE tripid = $tripId AND userid = $userId)));
                    """,
                    token,
                    identity);
                await ExecuteAsync(connection, transaction, "DELETE FROM localtripcontributions WHERE tripid = $tripId AND userid = $userId;", token, identity);
                await ExecuteAsync(connection, transaction, "DELETE FROM localtripdocuments WHERE tripid = $tripId AND userid = $userId;", token, identity);
                await ExecuteAsync(connection, transaction, "DELETE FROM localtrips WHERE id = $tripId AND userid = $userId;", token, identity);
                deleted = true;
            },
            cancellationToken);
        return deleted;
    }

    public async Task<IReadOnlyList<(Guid Id, string RelativePath)>> ListPendingFileDeletionsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _database.EnsureInitializedAsync(cancellationToken);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, relativepath FROM localpendingfiledeletions WHERE userid = $userId ORDER BY createdon;";
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        var rows = new List<(Guid, string)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add((Guid.Parse(reader.GetString(0)), reader.GetString(1)));
        }

        return rows;
    }

    public Task TryDeleteFileAsync(Guid userId, Guid id, string relativePath, Func<bool> delete, CancellationToken cancellationToken)
    {
        return _database.ExecuteInTransactionAsync(
            async (connection, transaction, token) =>
            {
                await using var check = connection.CreateCommand();
                check.Transaction = transaction;
                check.CommandText =
                    """
                    SELECT
                        (SELECT COUNT(*) FROM localcatchphotographs WHERE userid = $userId AND localfilepath = $path) +
                        (SELECT COUNT(*) FROM localtripcontributions WHERE userid = $userId AND localfilepath = $path);
                    """;
                check.Parameters.AddWithValue("$userId", userId.ToString("D"));
                check.Parameters.AddWithValue("$path", relativePath);
                if (Convert.ToInt32(await check.ExecuteScalarAsync(token)) == 0 && !delete())
                {
                    return;
                }

                await ExecuteAsync(
                    connection,
                    transaction,
                    "DELETE FROM localpendingfiledeletions WHERE id = $id AND userid = $userId AND relativepath = $path;",
                    token,
                    ("$id", id.ToString("D")),
                    ("$userId", userId.ToString("D")),
                    ("$path", relativePath));
            },
            cancellationToken);
    }

    private static void BindEligibility(SqliteCommand command, Guid userId)
    {
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        command.Parameters.AddWithValue("$completed", TripConstants.Completed);
        command.Parameters.AddWithValue("$cancelled", TripConstants.Cancelled);
    }

    private static async Task<LocalTripCleanupCandidateModel> ReadCandidateAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        Guid userId,
        Guid tripId,
        long revision,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT localfilepath
            FROM localtripcontributions
            WHERE tripid = $tripId AND userid = $userId;
            """;
        command.Parameters.AddWithValue("$tripId", tripId.ToString("D"));
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        var paths = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0) && reader.GetString(0).Length > 0)
            {
                paths.Add(reader.GetString(0));
            }
        }

        return new LocalTripCleanupCandidateModel
        {
            TripId = tripId,
            UserId = userId,
            Revision = revision,
            RelativeFilePaths = paths
        };
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
}
