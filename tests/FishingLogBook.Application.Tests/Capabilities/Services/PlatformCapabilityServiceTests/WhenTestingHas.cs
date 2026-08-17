using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Enums;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Capabilities.Services.PlatformCapabilityServiceTests;

public class WhenTestingHas : BasePlatformCapabilityServiceTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenTheRepositoryFails()
    {
        // Arrange
        var userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        MockUserPlatformCapabilityRepository
            .HasAsync(Arg.Any<FindUserPlatformCapabilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<bool>("Failed to persist platform capability."));

        // Act
        var result = await Sut.HasAsync(userId, PlatformCapabilityEnum.Guide, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to persist platform capability.");
        await MockUserPlatformCapabilityRepository.Received(1).HasAsync(
            Arg.Is<FindUserPlatformCapabilityArgs>(args =>
                args.UserId == userId && args.Capability == PlatformCapabilityEnum.Guide),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFalseWhenTheUserHasNoCapabilities()
    {
        // Arrange
        var userId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        MockUserPlatformCapabilityRepository
            .HasAsync(Arg.Any<FindUserPlatformCapabilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(false));

        // Act
        var result = await Sut.HasAsync(userId, PlatformCapabilityEnum.Administrator, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        await MockUserPlatformCapabilityRepository.Received(1).HasAsync(
            Arg.Is<FindUserPlatformCapabilityArgs>(args =>
                args.UserId == userId && args.Capability == PlatformCapabilityEnum.Administrator),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTrueWhenTheUserHasTheCapability()
    {
        // Arrange
        var userId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        MockUserPlatformCapabilityRepository
            .HasAsync(Arg.Any<FindUserPlatformCapabilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(true));

        // Act
        var result = await Sut.HasAsync(userId, PlatformCapabilityEnum.Guide, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        await MockUserPlatformCapabilityRepository.Received(1).HasAsync(
            Arg.Is<FindUserPlatformCapabilityArgs>(args =>
                args.UserId == userId && args.Capability == PlatformCapabilityEnum.Guide),
            Arg.Any<CancellationToken>());
    }
}
