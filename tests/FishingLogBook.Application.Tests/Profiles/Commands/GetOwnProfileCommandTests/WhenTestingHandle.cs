using AwesomeAssertions;
using FishingLogBook.Application.Profiles.Commands;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Commands.GetOwnProfileCommandTests;

public class WhenTestingHandle : BaseGetOwnProfileCommandTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new GetOwnProfileCommand { UserId = userId };
        MockProfileService
            .GetOrCreateOwnAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<ProfileDto>("Failed to load angler profile."));

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("Failed to load angler profile.");
        response.Profile.Should().BeNull();
        await MockProfileService.Received(1).GetOrCreateOwnAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheOwnProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new GetOwnProfileCommand { UserId = userId };
        var profile = OwnProfile(userId);
        MockProfileService
            .GetOrCreateOwnAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(profile));

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Profile.Should().Be(profile);
        await MockProfileService.Received(1).GetOrCreateOwnAsync(userId, Arg.Any<CancellationToken>());
    }
}
