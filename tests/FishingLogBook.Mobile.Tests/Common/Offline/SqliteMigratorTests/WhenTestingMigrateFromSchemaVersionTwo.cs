using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Sqlite;
using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Tests.Common.Offline.SqliteMigratorTests;

public sealed class WhenTestingMigrateFromSchemaVersionTwo : BaseSqliteMigratorTest
{
    private const string CatchId = "c0000000-0000-0000-0000-000000000001";
    private const string UserId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string OperationId = "e0000000-0000-0000-0000-000000000001";

    [Fact]
    public async Task ItShouldStillRejectASecondPendingOperationForTheSameKey()
    {
        // Arrange
        await CreateVersionTwoDatabaseAsync();
        await Sut.MigrateAsync(CancellationToken.None);
        await using var connection = await ConnectionFactory.OpenAsync(CancellationToken.None);

        // Act
        var act = async () => await ExecuteAsync(connection, InsertOutbox("e0000000-0000-0000-0000-000000000002"));

        // Assert
        await act.Should().ThrowAsync<SqliteException>();
        (await ScalarAsync(connection, "SELECT COUNT(*) FROM outboxoperations;")).Should().Be(1);
    }

    [Fact]
    public async Task ItShouldKeepExistingRowsWithNullLeaseColumnsAndCreateTheSyncStateSchema()
    {
        // Arrange
        await CreateVersionTwoDatabaseAsync();

        // Act
        await Sut.MigrateAsync(CancellationToken.None);

        // Assert
        await using var connection = await ConnectionFactory.OpenAsync(CancellationToken.None);
        (await ScalarAsync(connection, "SELECT version FROM schemainfo;")).Should().Be(3);

        await using var row = connection.CreateCommand();
        row.CommandText =
            "SELECT status, attemptcount, payloadjson, leaseowner, leaseexpiresat FROM outboxoperations WHERE id = $id;";
        row.Parameters.AddWithValue("$id", OperationId);
        await using (var reader = await row.ExecuteReaderAsync())
        {
            (await reader.ReadAsync()).Should().BeTrue();
            reader.GetString(0).Should().Be("Pending");
            reader.GetInt32(1).Should().Be(2);
            reader.GetString(2).Should().Be("{\"revision\":1}");
            reader.IsDBNull(3).Should().BeTrue();
            reader.IsDBNull(4).Should().BeTrue();
        }

        (await ScalarAsync(connection, "SELECT COUNT(*) FROM localcatches WHERE id = '" + CatchId + "';"))
            .Should().Be(1);
        (await ScalarAsync(
            connection,
            """
            SELECT COUNT(*) FROM sqlite_master
            WHERE (type = 'table' AND name IN ('localcatchsyncs', 'localphotographsyncs'))
               OR (type = 'index' AND name IN (
                    'ixoutboxoperationsclaim', 'ixlocalcatchsyncsuserid',
                    'ixlocalphotographsyncscatch', 'uxoutboxpendingcoalesce'));
            """)).Should().Be(6);
    }

    private async Task CreateVersionTwoDatabaseAsync()
    {
        await using var connection = await ConnectionFactory.OpenAsync(CancellationToken.None);
        await ExecuteAsync(connection, await ReadScriptAsync("001_InitialLocalPersistence.sql"));
        await ExecuteAsync(connection, "UPDATE schemainfo SET version = 1;");
        await ExecuteAsync(connection, await ReadScriptAsync("002_271_PhotographMetadata.sql"));
        await ExecuteAsync(connection, "UPDATE schemainfo SET version = 2;");
        await ExecuteAsync(
            connection,
            $"""
            INSERT INTO localcatches
                (id, userid, caughtbyuserid, recordedbyuserid, caughton, revision, lifecycle, createdon, updatedon)
            VALUES ('{CatchId}', '{UserId}', '{UserId}', '{UserId}', '2026-06-01T10:00:00Z', 1, 'Saved',
                    '2026-06-01T10:00:00Z', '2026-06-01T10:00:00Z');
            """);
        await ExecuteAsync(connection, InsertOutbox(OperationId));
        (await ScalarAsync(connection, "SELECT version FROM schemainfo;")).Should().Be(2);
    }

    private static string InsertOutbox(string id)
    {
        return
            $$"""
            INSERT INTO outboxoperations
                (id, userid, aggregatetype, aggregateid, operationtype, payloadjson, status,
                 attemptcount, createdon, availableat)
            VALUES ('{{id}}', '{{UserId}}', 'Catch', '{{CatchId}}', 'UpsertCatchMetadata', '{"revision":1}',
                    'Pending', 2, '2026-06-01T10:00:00Z', '2026-06-01T10:00:00Z');
            """;
    }

    private static async Task<string> ReadScriptAsync(string suffix)
    {
        var assembly = typeof(SqliteMigrator).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith(suffix, StringComparison.Ordinal));
        await using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
