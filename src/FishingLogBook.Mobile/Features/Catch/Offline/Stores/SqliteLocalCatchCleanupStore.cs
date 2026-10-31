using FishingLogBook.Mobile.Common.Offline.Sqlite;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Features.Catch.Offline.Stores;

public sealed class SqliteLocalCatchCleanupStore : ILocalCatchCleanupStore
{
    private const string EligibleCatchSql =
        """
        c.userid = $userId
        AND c.lifecycle = 'Saved'
        AND EXISTS (SELECT 1 FROM localcatchsyncs s
                    WHERE s.catchid = c.id AND s.userid = c.userid AND s.syncedrevision >= c.revision)
        AND EXISTS (SELECT 1 FROM localcatchphotographs p WHERE p.catchid = c.id AND p.userid = c.userid)
        AND NOT EXISTS (SELECT 1 FROM localcatchphotographs p
                        LEFT JOIN localphotographsyncs ps ON ps.photographid = p.id AND ps.userid = p.userid
                        WHERE p.catchid = c.id AND p.userid = c.userid
                          AND (ps.status IS NULL OR ps.status <> 'Recorded'))
        AND NOT EXISTS (SELECT 1 FROM outboxoperations o
                        WHERE o.userid = c.userid AND o.status <> 'Completed'
                          AND ((o.aggregatetype = 'Catch' AND o.aggregateid = c.id)
                               OR (o.aggregatetype = 'CatchPhotograph' AND o.aggregateid IN
                                   (SELECT p.id FROM localcatchphotographs p
                                    WHERE p.catchid = c.id AND p.userid = c.userid))))
        """;

    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly ISqliteDatabase _database;

    public SqliteLocalCatchCleanupStore(ISqliteConnectionFactory connectionFactory, ISqliteDatabase database)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(database);
        _connectionFactory = connectionFactory;
        _database = database;
    }

    public async Task<IReadOnlyList<LocalCatchCleanupCandidateModel>> ListCandidatesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _database.EnsureInitializedAsync(cancellationToken);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        var catches = new List<(Guid Id, long Revision)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"SELECT c.id, c.revision FROM localcatches c WHERE {EligibleCatchSql} ORDER BY c.updatedon;";
            command.Parameters.AddWithValue("$userId", userId.ToString("D"));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                catches.Add((Guid.Parse(reader.GetString(0)), reader.GetInt64(1)));
            }
        }

        var candidates = new List<LocalCatchCleanupCandidateModel>(catches.Count);
        foreach (var (id, revision) in catches)
        {
            candidates.Add(await ReadCandidateAsync(connection, null, userId, id, revision, cancellationToken));
        }

        return candidates;
    }

    public async Task<bool> TryDeleteAsync(
        LocalCatchCleanupCandidateModel candidate,
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
                    $"SELECT COUNT(*) FROM localcatches c WHERE c.id = $catchId AND c.revision = $revision AND {EligibleCatchSql};";
                check.Parameters.AddWithValue("$catchId", candidate.CatchId.ToString("D"));
                check.Parameters.AddWithValue("$revision", candidate.Revision);
                check.Parameters.AddWithValue("$userId", candidate.UserId.ToString("D"));
                if (Convert.ToInt32(await check.ExecuteScalarAsync(token)) != 1)
                {
                    return;
                }

                var current = await ReadCandidateAsync(connection, transaction, candidate.UserId, candidate.CatchId, candidate.Revision, token);
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
                    ("$catchId", candidate.CatchId.ToString("D")),
                    ("$userId", candidate.UserId.ToString("D"))
                };
                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    DELETE FROM outboxoperations
                    WHERE userid = $userId AND status = 'Completed'
                      AND ((aggregatetype = 'Catch' AND aggregateid = $catchId)
                           OR (aggregatetype = 'CatchPhotograph' AND aggregateid IN
                               (SELECT id FROM localcatchphotographs WHERE catchid = $catchId AND userid = $userId)));
                    """,
                    token,
                    identity);
                await ExecuteAsync(connection, transaction, "DELETE FROM localphotographsyncs WHERE catchid = $catchId AND userid = $userId;", token, identity);
                await ExecuteAsync(connection, transaction, "DELETE FROM localcatchphotographs WHERE catchid = $catchId AND userid = $userId;", token, identity);
                await ExecuteAsync(connection, transaction, "DELETE FROM localcatchsyncs WHERE catchid = $catchId AND userid = $userId;", token, identity);
                await ExecuteAsync(connection, transaction, "DELETE FROM localcatches WHERE id = $catchId AND userid = $userId;", token, identity);
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

    public async Task<bool> IsFileReferencedAsync(Guid userId, string relativePath, CancellationToken cancellationToken)
    {
        await _database.EnsureInitializedAsync(cancellationToken);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM localcatchphotographs WHERE userid = $userId AND localfilepath = $path;";
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        command.Parameters.AddWithValue("$path", relativePath);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    public Task CompleteFileDeletionAsync(Guid id, CancellationToken cancellationToken)
    {
        return _database.ExecuteInTransactionAsync(
            async (connection, transaction, token) =>
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    "DELETE FROM localpendingfiledeletions WHERE id = $id;",
                    token,
                    ("$id", id.ToString("D")));
            },
            cancellationToken);
    }

    private static async Task<LocalCatchCleanupCandidateModel> ReadCandidateAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        Guid userId,
        Guid catchId,
        long revision,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT id, localfilepath FROM localcatchphotographs WHERE catchid = $catchId AND userid = $userId ORDER BY sortorder;";
        command.Parameters.AddWithValue("$catchId", catchId.ToString("D"));
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        var ids = new List<Guid>();
        var paths = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(Guid.Parse(reader.GetString(0)));
            if (!reader.IsDBNull(1) && reader.GetString(1).Length > 0)
            {
                paths.Add(reader.GetString(1));
            }
        }

        return new LocalCatchCleanupCandidateModel
        {
            CatchId = catchId,
            UserId = userId,
            Revision = revision,
            PhotographIds = ids,
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
