using AwesomeAssertions;
using FishingLogBook.Mobile.Features.LocalPersistence.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Mobile.Tests.Features.LocalPersistence.SqliteMigratorTests;

public sealed class WhenTestingMigrate : BaseSqliteMigratorTest
{
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
        Convert.ToInt32(await versionCommand.ExecuteScalarAsync()).Should().Be(1);

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
        Convert.ToInt32(await versionCommand.ExecuteScalarAsync()).Should().Be(1);
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
