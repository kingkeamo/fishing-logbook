using AwesomeAssertions;
using Dapper;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Migrations.SchemaTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingTripCollaborationSchema
{
    private readonly PostgresFixture _fixture;

    public WhenTestingTripCollaborationSchema(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ItShouldExposeTheTripParticipantTable()
    {
        // Arrange
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);

        // Act
        var columns = await connection.QueryAsync<string>(
            """
            SELECT "column_name"
            FROM information_schema.columns
            WHERE "table_schema" = 'public' AND "table_name" = 'TripParticipant';
            """);

        // Assert
        columns.Should().Contain(
        [
            "Id",
            "TripId",
            "UserId",
            "Status",
            "InvitedByUserId",
            "InvitedOn",
            "RespondedOn",
            "RemovedOn"
        ]);
    }

    [Fact]
    public async Task ItShouldKeepOneMembershipRowPerAnglerAndTrip()
    {
        // Arrange
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);

        // Act
        var unique = await connection.QueryAsync<string>(
            """
            SELECT "indexname"
            FROM pg_indexes
            WHERE "schemaname" = 'public' AND "tablename" = 'TripParticipant';
            """);

        // Assert
        unique.Should().Contain("UxTripParticipantTripUser");
    }

    [Fact]
    public async Task ItShouldRequireAContributorOnEveryTripPhotograph()
    {
        // Arrange
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);

        // Act
        var nullable = await connection.QuerySingleOrDefaultAsync<string>(
            """
            SELECT "is_nullable"
            FROM information_schema.columns
            WHERE "table_schema" = 'public'
              AND "table_name" = 'TripPhotograph'
              AND "column_name" = 'ContributedByUserId';
            """);

        // Assert
        nullable.Should().Be("NO");
    }

    [Fact]
    public async Task ItShouldAllowOnlyTheKnownMembershipStatuses()
    {
        // Arrange
        var connectionFactory = new NpgsqlConnectionFactory(_fixture.ConnectionString);
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);

        // Act
        var check = await connection.QuerySingleOrDefaultAsync<string>(
            """
            SELECT pg_get_constraintdef("oid")
            FROM pg_constraint
            WHERE "conname" = 'TripParticipant_Status_Allowed';
            """);

        // Assert
        check.Should().NotBeNull();
        check.Should().Contain("Pending");
        check.Should().Contain("Accepted");
        check.Should().Contain("Declined");
    }
}
