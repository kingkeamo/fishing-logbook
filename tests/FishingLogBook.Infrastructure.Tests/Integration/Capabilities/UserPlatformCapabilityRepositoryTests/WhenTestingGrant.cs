using AwesomeAssertions;
using Dapper;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Infrastructure.Tests.Integration.TestSupport;
using Npgsql;

namespace FishingLogBook.Infrastructure.Tests.Integration.Capabilities.UserPlatformCapabilityRepositoryTests;

public class WhenTestingGrant : BaseUserPlatformCapabilityRepositoryTest
{
    public WhenTestingGrant(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldFailWhenTheUserDoesNotExist()
    {
        // Arrange
        var missingUserId = Guid.NewGuid();

        // Act
        var result = await Sut.GrantAsync(
            Association(missingUserId, PlatformCapabilityEnum.Guide),
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Platform capability is invalid.");
        (await CountForUserAsync(missingUserId)).Should().Be(0);
    }

    [Fact]
    public async Task ItShouldRejectAnUnknownCapabilityCode()
    {
        // Arrange
        var userId = await CreateUserAsync();
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        var act = () => connection.ExecuteAsync(
            """
            INSERT INTO "UserPlatformCapability" ("UserId", "CapabilityCode")
            VALUES (@UserId, @CapabilityCode);
            """,
            new { UserId = userId, CapabilityCode = "Angler" });

        // Act
        // Assert
        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
        (await CountForUserAsync(userId)).Should().Be(0);
    }

    [Fact]
    public async Task ItShouldRejectAClubScopedCapabilityCode()
    {
        // Arrange
        var userId = await CreateUserAsync();
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        var act = () => connection.ExecuteAsync(
            """
            INSERT INTO "UserPlatformCapability" ("UserId", "CapabilityCode")
            VALUES (@UserId, @CapabilityCode);
            """,
            new { UserId = userId, CapabilityCode = "ClubAdmin" });

        // Act
        // Assert
        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
        (await CountForUserAsync(userId)).Should().Be(0);
    }

    [Fact]
    public async Task ItShouldRejectADuplicateUserCapabilityPair()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var granted = await Sut.GrantAsync(Association(userId, PlatformCapabilityEnum.Guide), CancellationToken.None);
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        var act = () => connection.ExecuteAsync(
            """
            INSERT INTO "UserPlatformCapability" ("UserId", "CapabilityCode")
            VALUES (@UserId, @CapabilityCode);
            """,
            new { UserId = userId, CapabilityCode = nameof(PlatformCapabilityEnum.Guide) });

        // Act
        // Assert
        granted.IsSuccess.Should().BeTrue();
        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        (await CountForUserCapabilityAsync(userId, PlatformCapabilityEnum.Guide)).Should().Be(1);
    }

    [Fact]
    public async Task ItShouldSeedOnlySpecialistCapabilities()
    {
        // Arrange
        // Act
        var codes = await SeededCodesAsync();

        // Assert
        codes.Should().Equal(
            nameof(PlatformCapabilityEnum.Administrator),
            nameof(PlatformCapabilityEnum.CompetitionOrganiser),
            nameof(PlatformCapabilityEnum.FishingVenueManager),
            nameof(PlatformCapabilityEnum.Guide));
        codes.Should().NotContain("Angler");
        codes.Should().NotContain("ClubAdmin");
        codes.Should().NotContain("ClubMember");
        codes.Should().NotContain("ClubOfficer");
        codes.Should().NotContain("ClubCompetitionOrganiser");
    }

    [Fact]
    public async Task ItShouldGrantIdempotently()
    {
        // Arrange
        var userId = await CreateUserAsync();

        // Act
        var first = await Sut.GrantAsync(Association(userId, PlatformCapabilityEnum.Guide), CancellationToken.None);
        var second = await Sut.GrantAsync(Association(userId, PlatformCapabilityEnum.Guide), CancellationToken.None);

        // Assert
        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        (await CountForUserCapabilityAsync(userId, PlatformCapabilityEnum.Guide)).Should().Be(1);
        (await CountForUserAsync(userId)).Should().Be(1);
    }

    [Fact]
    public async Task ItShouldAllowTheSameCapabilityOnDifferentUsers()
    {
        // Arrange
        var userA = await CreateUserAsync();
        var userB = await CreateUserAsync();

        // Act
        var grantedA = await Sut.GrantAsync(Association(userA, PlatformCapabilityEnum.Guide), CancellationToken.None);
        var grantedB = await Sut.GrantAsync(Association(userB, PlatformCapabilityEnum.Guide), CancellationToken.None);

        // Assert
        grantedA.IsSuccess.Should().BeTrue();
        grantedB.IsSuccess.Should().BeTrue();
        (await CountForUserCapabilityAsync(userA, PlatformCapabilityEnum.Guide)).Should().Be(1);
        (await CountForUserCapabilityAsync(userB, PlatformCapabilityEnum.Guide)).Should().Be(1);
    }

    [Fact]
    public async Task ItShouldPersistMultipleCapabilitiesForTheSameUser()
    {
        // Arrange
        var userId = await CreateUserAsync();

        // Act
        var guide = await Sut.GrantAsync(Association(userId, PlatformCapabilityEnum.Guide), CancellationToken.None);
        var organiser = await Sut.GrantAsync(
            Association(userId, PlatformCapabilityEnum.CompetitionOrganiser),
            CancellationToken.None);
        var administrator = await Sut.GrantAsync(
            Association(userId, PlatformCapabilityEnum.Administrator),
            CancellationToken.None);

        // Assert
        guide.IsSuccess.Should().BeTrue();
        organiser.IsSuccess.Should().BeTrue();
        administrator.IsSuccess.Should().BeTrue();
        (await CountForUserAsync(userId)).Should().Be(3);
        (await CountForUserCapabilityAsync(userId, PlatformCapabilityEnum.Guide)).Should().Be(1);
        (await CountForUserCapabilityAsync(userId, PlatformCapabilityEnum.CompetitionOrganiser)).Should().Be(1);
        (await CountForUserCapabilityAsync(userId, PlatformCapabilityEnum.Administrator)).Should().Be(1);
    }

    [Fact]
    public async Task ItShouldPreventDeletingAUserThatHasCapabilities()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var granted = await Sut.GrantAsync(Association(userId, PlatformCapabilityEnum.Guide), CancellationToken.None);
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        await connection.ExecuteAsync(
            """DELETE FROM "UserIdentity" WHERE "UserId" = @Id;""",
            new { Id = userId });

        try
        {
            var act = () => connection.ExecuteAsync(
                """DELETE FROM "User" WHERE "Id" = @Id;""",
                new { Id = userId });

            // Act
            // Assert
            granted.IsSuccess.Should().BeTrue();
            var exception = await act.Should().ThrowAsync<PostgresException>();
            exception.Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
            (await CountForUserCapabilityAsync(userId, PlatformCapabilityEnum.Guide)).Should().Be(1);
        }
        finally
        {
            await connection.ExecuteAsync(
                """DELETE FROM "UserPlatformCapability" WHERE "UserId" = @Id;""",
                new { Id = userId });
            await connection.ExecuteAsync(
                """DELETE FROM "User" WHERE "Id" = @Id;""",
                new { Id = userId });
        }
    }
}
