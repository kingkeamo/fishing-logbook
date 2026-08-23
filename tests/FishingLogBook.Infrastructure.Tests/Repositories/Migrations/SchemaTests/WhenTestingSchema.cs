using AwesomeAssertions;
using Dapper;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Migrations.SchemaTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingSchema
{
    private readonly PostgresFixture _fixture;

    public WhenTestingSchema(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ItShouldNotExposeTheObsoleteTestCatchTables()
    {
        // Arrange
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);

        // Act
        var tables = await connection.QueryAsync<string>(
            """SELECT "table_name" FROM information_schema.tables WHERE "table_schema" = 'public';""");

        // Assert
        var tableNames = tables.ToArray();
        tableNames.Should().NotContain("TestCatch");
        tableNames.Should().NotContain("TestCatchPhotograph");
    }

    [Fact]
    public async Task ItShouldStillExposeTheRealCatchTables()
    {
        // Arrange
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);

        // Act
        var tables = await connection.QueryAsync<string>(
            """SELECT "table_name" FROM information_schema.tables WHERE "table_schema" = 'public';""");

        // Assert
        var tableNames = tables.ToArray();
        tableNames.Should().Contain("Catch");
        tableNames.Should().Contain("CatchPhotograph");
    }

    [Fact]
    public async Task ItShouldRemoveTheLegacyProfileFishingArrayColumns()
    {
        // Arrange
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);

        // Act
        var columns = await connection.QueryAsync<string>(
            """
            SELECT "column_name" FROM information_schema.columns
            WHERE "table_schema" = 'public' AND "table_name" = 'Profile';
            """);

        // Assert
        var columnNames = columns.ToArray();
        columnNames.Should().NotContain("PreferredFishingTypes");
        columnNames.Should().NotContain("PreferredSpecies");
        columnNames.Should().NotContain("ShowPreferredFishingTypes");
        columnNames.Should().Contain("ShowPreferredFishingMethods");
        columnNames.Should().Contain("ShowPreferredSpecies");
    }

    [Fact]
    public async Task ItShouldStillExposeTheCanonicalFishingPreferenceTables()
    {
        // Arrange
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);

        // Act
        var tables = await connection.QueryAsync<string>(
            """SELECT "table_name" FROM information_schema.tables WHERE "table_schema" = 'public';""");

        // Assert
        var tableNames = tables.ToArray();
        tableNames.Should().Contain("FishingMethod");
        tableNames.Should().Contain("Species");
        tableNames.Should().Contain("UserFishingMethodPreference");
        tableNames.Should().Contain("UserFishingSpeciesPreference");
    }

    [Fact]
    public async Task ItShouldExposeNullableOnboardingCompletion()
    {
        // Arrange
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);

        // Act
        var nullable = await connection.QuerySingleAsync<string>(
            """
            SELECT "is_nullable" FROM information_schema.columns
            WHERE "table_schema" = 'public'
              AND "table_name" = 'Profile'
              AND "column_name" = 'OnboardingCompletedOn';
            """);

        // Assert
        nullable.Should().Be("YES");
    }

    [Fact]
    public async Task ItShouldExposeOfflineAccessPreferenceAndServerTimestamp()
    {
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);

        var columns = await connection.QueryAsync<(string Name, string Nullable)>(
            """
            SELECT "column_name" AS "Name", "is_nullable" AS "Nullable"
            FROM information_schema.columns
            WHERE "table_schema" = 'public' AND "table_name" = 'User'
              AND "column_name" IN ('OfflineAccessEnabled', 'OfflineAccessEnabledAt');
            """);

        columns.Should().Contain(column =>
            column.Name == "OfflineAccessEnabled" && column.Nullable == "NO");
        columns.Should().Contain(column =>
            column.Name == "OfflineAccessEnabledAt" && column.Nullable == "YES");
    }
}
