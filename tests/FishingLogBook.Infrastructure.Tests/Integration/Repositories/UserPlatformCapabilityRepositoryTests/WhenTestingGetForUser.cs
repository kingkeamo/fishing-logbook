using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.UserPlatformCapabilityRepositoryTests;

public class WhenTestingGetForUser : BaseUserPlatformCapabilityRepositoryTest
{
    public WhenTestingGetForUser(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldReturnEmptyWhenTheUserHasNoCapabilities()
    {
        // Arrange
        var userId = await CreateUserAsync();

        // Act
        var result = await Sut.GetForUserAsync(
            new FindUserPlatformCapabilitiesArgs { UserId = userId },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotReturnAnotherUsersCapabilities()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var granted = await Sut.GrantAsync(
            Association(otherUserId, PlatformCapabilityEnum.Guide),
            CancellationToken.None);

        // Act
        var result = await Sut.GetForUserAsync(
            new FindUserPlatformCapabilitiesArgs { UserId = userId },
            CancellationToken.None);

        // Assert
        granted.IsSuccess.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReturnAllCapabilitiesForTheUser()
    {
        // Arrange
        var userId = await CreateUserAsync();
        await Sut.GrantAsync(Association(userId, PlatformCapabilityEnum.Guide), CancellationToken.None);
        await Sut.GrantAsync(
            Association(userId, PlatformCapabilityEnum.CompetitionOrganiser),
            CancellationToken.None);
        await Sut.GrantAsync(Association(userId, PlatformCapabilityEnum.Administrator), CancellationToken.None);

        // Act
        var result = await Sut.GetForUserAsync(
            new FindUserPlatformCapabilitiesArgs { UserId = userId },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(
        [
            PlatformCapabilityEnum.Guide,
            PlatformCapabilityEnum.CompetitionOrganiser,
            PlatformCapabilityEnum.Administrator
        ]);
    }
}
