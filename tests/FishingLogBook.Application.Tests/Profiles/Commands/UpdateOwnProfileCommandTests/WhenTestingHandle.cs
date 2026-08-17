using AwesomeAssertions;
using FishingLogBook.Application.Args;
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
        var command = Command(userId);
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
                && args.PreferredFishingTypes.SequenceEqual(new[] { "Coarse" })
                && args.PreferredSpecies.SequenceEqual(new[] { "Pike" })
                && args.ShowDisplayName
                && !args.ShowPhotograph
                && args.ShowHomeRegion
                && args.ShowPreferredFishingTypes
                && !args.ShowPreferredSpecies),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheUpdatedProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = Command(userId);
        var saved = OwnProfile(userId);
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
                && args.HomeRegion == "Westmeath"
                && args.PreferredFishingTypes.SequenceEqual(new[] { "Coarse" })
                && args.PreferredSpecies.SequenceEqual(new[] { "Pike" })),
            Arg.Any<CancellationToken>());
    }
}
