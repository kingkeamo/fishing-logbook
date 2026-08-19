using AwesomeAssertions;
using Dapper;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Tests.Integration.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Integration.Migrations.SchemaTests;

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
}
