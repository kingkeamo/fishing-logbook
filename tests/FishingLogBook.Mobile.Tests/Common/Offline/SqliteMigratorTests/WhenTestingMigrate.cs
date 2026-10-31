using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Mobile.Tests.Common.Offline.SqliteMigratorTests;

public sealed class WhenTestingMigrate : BaseSqliteMigratorTest
{
    [Fact]
    public async Task ItShouldUpgradeExistingPhotoReferencesWithoutLosingTheirData()
    {
        // Arrange
        var assembly = typeof(SqliteMigrator).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("001_InitialLocalPersistence.sql"));
        await using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        var initialSql = await reader.ReadToEndAsync();
        await using var connection = await ConnectionFactory.OpenAsync(CancellationToken.None);
        await using var setup = connection.CreateCommand();
        setup.CommandText = initialSql +
            """
            UPDATE schemainfo SET version = 1;
            INSERT INTO localcatches
                (id, userid, caughtbyuserid, recordedbyuserid, caughton, revision, lifecycle, createdon, updatedon)
            VALUES ('catch', 'owner', 'owner', 'owner', '2026-06-01T10:00:00Z', 1, 'Saved', '2026-06-01T10:00:00Z', '2026-06-01T10:00:00Z');
            INSERT INTO localcatchphotographs
                (id, catchid, userid, sortorder, localfilepath, contenttype, createdon)
            VALUES ('photo', 'catch', 'owner', 0, 'owner/photo.jpg', 'image/jpeg', '2026-06-01T10:00:00Z');
            """;
        await setup.ExecuteNonQueryAsync();

        // Act
        await Sut.MigrateAsync(CancellationToken.None);
        await Sut.MigrateAsync(CancellationToken.None);

        // Assert
        await using var query = connection.CreateCommand();
        query.CommandText = "SELECT localfilepath, contenttype, metadatajson FROM localcatchphotographs WHERE id = 'photo';";
        await using var loaded = await query.ExecuteReaderAsync();
        (await loaded.ReadAsync()).Should().BeTrue();
        loaded.GetString(0).Should().Be("owner/photo.jpg");
        loaded.GetString(1).Should().Be("image/jpeg");
        loaded.IsDBNull(2).Should().BeTrue();
        (await loaded.ReadAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldApplyTheInitialMigrationOnAnEmptyDatabase()
    {
        // Arrange

        // Act
        await Sut.MigrateAsync(CancellationToken.None);

        // Assert
        await using var connection = await ConnectionFactory.OpenAsync(CancellationToken.None);
        await using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "SELECT version FROM schemainfo;";
        Convert.ToInt32(await versionCommand.ExecuteScalarAsync()).Should().Be(2);

        await using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText =
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name IN ('localcatches', 'localcatchphotographs', 'localtrips', 'outboxoperations');
            """;
        Convert.ToInt32(await tableCommand.ExecuteScalarAsync()).Should().Be(4);
    }

    [Fact]
    public async Task ItShouldBeIdempotentWhenMigrateIsCalledAgain()
    {
        // Arrange
        await Sut.MigrateAsync(CancellationToken.None);

        // Act
        await Sut.MigrateAsync(CancellationToken.None);
        await Sut.MigrateAsync(CancellationToken.None);

        // Assert
        await using var connection = await ConnectionFactory.OpenAsync(CancellationToken.None);
        await using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "SELECT version FROM schemainfo;";
        Convert.ToInt32(await versionCommand.ExecuteScalarAsync()).Should().Be(2);
    }

    [Fact]
    public async Task ItShouldFailClosedAndLeaveNoPartialSchemaWhenAMigrationScriptFails()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), "flb-sqlite-bad-migrate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "broken.db");
        var factory = new SqliteConnectionFactory(path);
        var migrator = new SqliteMigrator(
            factory,
            NullLogger<SqliteMigrator>.Instance,
            typeof(WhenTestingMigrate).Assembly);

        // Act
        var act = async () => await migrator.MigrateAsync(CancellationToken.None);

        // Assert
        try
        {
            await act.Should().ThrowAsync<SqliteException>();
            await using var connection = await factory.OpenAsync(CancellationToken.None);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table' AND name = 'schemainfo';
                """;
            Convert.ToInt32(await command.ExecuteScalarAsync()).Should().Be(0);
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
