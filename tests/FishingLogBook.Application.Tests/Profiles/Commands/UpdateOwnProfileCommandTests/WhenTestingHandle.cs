using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Profiles.Commands;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Commands.UpdateOwnProfileCommandTests;

public class WhenTestingHandle : BaseUpdateOwnProfileCommandTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = Command(userId, PrivateLocation());
        MockProfileService
            .UpdateOwnAsync(Arg.Any<UpdateProfileArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<ProfileDto>("Failed to load angler profile."));

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("Failed to load angler profile.");
        response.Profile.Should().BeNull();
        await MockProfileService.Received(1).UpdateOwnAsync(
            Arg.Is<UpdateProfileArgs>(args =>
                args.UserId == userId
                && args.DisplayName == "Eamonn"
                && args.HomeRegion == "Westmeath"
                && args.Location != null
                && args.Location.Visibility == LocationDefaults.Private),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheUpdatedProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var location = PrivateLocation();
        var command = Command(userId, location);
        var saved = new ProfileDto(
            userId,
            "Eamonn",
            null,
            null,
            null,
            "Westmeath",
            ["Coarse"],
            ["Pike"],
            true,
            false,
            true,
            true,
            false,
            location);
        MockProfileService
            .UpdateOwnAsync(Arg.Any<UpdateProfileArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(saved));

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Profile.Should().Be(saved);
        await MockProfileService.Received(1).UpdateOwnAsync(
            Arg.Is<UpdateProfileArgs>(args =>
                args.UserId == userId
                && args.DisplayName == "Eamonn"
                && args.PreferredFishingTypes.SequenceEqual(new[] { "Coarse" })
                && args.PreferredSpecies.SequenceEqual(new[] { "Pike" })
                && args.Location != null
                && args.Location.Latitude == 53.4
                && args.Location.Visibility == LocationDefaults.Private),
            Arg.Any<CancellationToken>());
    }
}
