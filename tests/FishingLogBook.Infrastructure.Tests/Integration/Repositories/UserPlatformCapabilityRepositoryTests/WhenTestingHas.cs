using AwesomeAssertions;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.UserPlatformCapabilityRepositoryTests;

public class WhenTestingHas : BaseUserPlatformCapabilityRepositoryTest
{
    public WhenTestingHas(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldReturnFalseWhenTheUserHasNoCapabilities()
    {
        // Arrange
        var userId = await CreateUserAsync();

        // Act
        var result = await Sut.HasAsync(Lookup(userId, PlatformCapabilityEnum.Guide), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldReturnFalseForADifferentCapability()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var granted = await Sut.GrantAsync(Association(userId, PlatformCapabilityEnum.Guide), CancellationToken.None);

        // Act
        var result = await Sut.HasAsync(
            Lookup(userId, PlatformCapabilityEnum.Administrator),
            CancellationToken.None);

        // Assert
        granted.IsSuccess.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldReturnTrueWhenTheCapabilityExists()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var granted = await Sut.GrantAsync(Association(userId, PlatformCapabilityEnum.Guide), CancellationToken.None);

        // Act
        var result = await Sut.HasAsync(Lookup(userId, PlatformCapabilityEnum.Guide), CancellationToken.None);

        // Assert
        granted.IsSuccess.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }
}
