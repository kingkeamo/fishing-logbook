using AwesomeAssertions;
using FishingLogBook.Application.Profiles.Queries;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Queries.GetOwnProfileQueryTests;

public class WhenTestingHandle : BaseGetOwnProfileQueryTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetOwnProfileQuery { UserId = userId };
        MockProfileService
            .GetOwnAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<ProfileDto>("Failed to load angler profile."));

        // Act
        var response = await Sut.Handle(query, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("Failed to load angler profile.");
        response.Profile.Should().BeNull();
        await MockProfileService.Received(1).GetOwnAsync(userId, Arg.Any<CancellationToken>());
        await MockProfileService.DidNotReceive().UpdateOwnAsync(
            Arg.Any<FishingLogBook.Application.Args.UpdateProfileArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheOwnProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetOwnProfileQuery { UserId = userId };
        var profile = OwnProfile(userId);
        MockProfileService
            .GetOwnAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(profile));

        // Act
        var response = await Sut.Handle(query, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Profile.Should().Be(profile);
        await MockProfileService.Received(1).GetOwnAsync(userId, Arg.Any<CancellationToken>());
        await MockProfileService.DidNotReceive().UpdateOwnAsync(
            Arg.Any<FishingLogBook.Application.Args.UpdateProfileArgs>(),
            Arg.Any<CancellationToken>());
    }
}
