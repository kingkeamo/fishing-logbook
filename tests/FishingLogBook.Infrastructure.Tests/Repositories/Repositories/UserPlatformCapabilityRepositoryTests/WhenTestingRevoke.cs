using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.UserPlatformCapabilityRepositoryTests;

public class WhenTestingRevoke : BaseUserPlatformCapabilityRepositoryTest
{
    public WhenTestingRevoke(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldSucceedWhenTheCapabilityIsNotPresent()
    {
        // Arrange
        var userId = await CreateUserAsync();

        // Act
        var result = await Sut.RevokeAsync(Lookup(userId, PlatformCapabilityEnum.Guide), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        (await CountForUserAsync(userId)).Should().Be(0);
    }

    [Fact]
    public async Task ItShouldLeaveUnrelatedCapabilitiesInPlace()
    {
        // Arrange
        var userId = await CreateUserAsync();
        await Sut.GrantAsync(Association(userId, PlatformCapabilityEnum.Guide), CancellationToken.None);
        await Sut.GrantAsync(
            Association(userId, PlatformCapabilityEnum.CompetitionOrganiser),
            CancellationToken.None);

        // Act
        var result = await Sut.RevokeAsync(Lookup(userId, PlatformCapabilityEnum.Guide), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        (await CountForUserCapabilityAsync(userId, PlatformCapabilityEnum.Guide)).Should().Be(0);
        (await CountForUserCapabilityAsync(userId, PlatformCapabilityEnum.CompetitionOrganiser)).Should().Be(1);
        (await CountForUserAsync(userId)).Should().Be(1);
        var remaining = await Sut.GetForUserAsync(
            new FindUserPlatformCapabilitiesArgs { UserId = userId },
            CancellationToken.None);
        remaining.Value.Should().Equal(PlatformCapabilityEnum.CompetitionOrganiser);
    }

    [Fact]
    public async Task ItShouldRemoveOnlyTheRequestedAssociation()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        await Sut.GrantAsync(Association(userId, PlatformCapabilityEnum.Guide), CancellationToken.None);
        await Sut.GrantAsync(Association(otherUserId, PlatformCapabilityEnum.Guide), CancellationToken.None);

        // Act
        var result = await Sut.RevokeAsync(Lookup(userId, PlatformCapabilityEnum.Guide), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        (await CountForUserCapabilityAsync(userId, PlatformCapabilityEnum.Guide)).Should().Be(0);
        (await CountForUserCapabilityAsync(otherUserId, PlatformCapabilityEnum.Guide)).Should().Be(1);
    }
}
