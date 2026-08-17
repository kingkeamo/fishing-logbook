using AwesomeAssertions;
using FishingLogBook.Application.Profiles.Queries;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Queries.GetPublicProfileQueryTests;

public class WhenTestingHandle : BaseGetPublicProfileQueryTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetPublicProfileQuery { UserId = userId };
        MockProfileService
            .GetPublicAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<PublicProfileDto>("Angler profile was not found."));

        // Act
        var response = await Sut.Handle(query, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("Angler profile was not found.");
        response.Profile.Should().BeNull();
        await MockProfileService.Received(1).GetPublicAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnThePublicProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetPublicProfileQuery { UserId = userId };
        var profile = PublicProfile(userId);
        MockProfileService
            .GetPublicAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(profile));

        // Act
        var response = await Sut.Handle(query, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Profile.Should().Be(profile);
        typeof(PublicProfileDto).GetProperty("Location").Should().BeNull();
        typeof(PublicProfileDto).GetProperty("Latitude").Should().BeNull();
        await MockProfileService.Received(1).GetPublicAsync(userId, Arg.Any<CancellationToken>());
    }
}
