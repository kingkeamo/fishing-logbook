using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Enums;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Capabilities.Services.PlatformCapabilityServiceTests;

public class WhenTestingGetForUser : BasePlatformCapabilityServiceTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenTheRepositoryFails()
    {
        // Arrange
        var userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        MockUserPlatformCapabilityRepository
            .GetForUserAsync(Arg.Any<FindUserPlatformCapabilitiesArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<PlatformCapabilityEnum>>("Failed to persist platform capability."));

        // Act
        var result = await Sut.GetForUserAsync(userId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        await MockUserPlatformCapabilityRepository.Received(1).GetForUserAsync(
            Arg.Is<FindUserPlatformCapabilitiesArgs>(args => args.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnAnEmptyListWhenTheUserHasNoCapabilities()
    {
        // Arrange
        var userId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        MockUserPlatformCapabilityRepository
            .GetForUserAsync(Arg.Any<FindUserPlatformCapabilitiesArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<PlatformCapabilityEnum>>([]));

        // Act
        var result = await Sut.GetForUserAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        await MockUserPlatformCapabilityRepository.Received(1).GetForUserAsync(
            Arg.Is<FindUserPlatformCapabilitiesArgs>(args => args.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnThePersistedCapabilities()
    {
        // Arrange
        var userId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        IReadOnlyList<PlatformCapabilityEnum> capabilities =
        [
            PlatformCapabilityEnum.Guide,
            PlatformCapabilityEnum.CompetitionOrganiser
        ];
        MockUserPlatformCapabilityRepository
            .GetForUserAsync(Arg.Any<FindUserPlatformCapabilitiesArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(capabilities));

        // Act
        var result = await Sut.GetForUserAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(capabilities);
        await MockUserPlatformCapabilityRepository.Received(1).GetForUserAsync(
            Arg.Is<FindUserPlatformCapabilitiesArgs>(args => args.UserId == userId),
            Arg.Any<CancellationToken>());
    }
}
