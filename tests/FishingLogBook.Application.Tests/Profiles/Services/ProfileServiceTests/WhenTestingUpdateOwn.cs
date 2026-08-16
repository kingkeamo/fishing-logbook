using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Tests.Common.Builders;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Services.ProfileServiceTests;

public class WhenTestingUpdateOwn : BaseProfileServiceTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenTheLookupFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var args = new UpdateProfileArgs { UserId = userId, DisplayName = "Eamonn" };
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Profile?>("Failed to load angler profile."));

        // Act
        var result = await Sut.UpdateOwnAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to load angler profile.");
        await MockProfileRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Profile>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDefaultBlankVisibilityToPrivate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var args = new UpdateProfileArgs
        {
            UserId = userId,
            Location = new CatchLocationDto(
                53.4,
                -7.9,
                12,
                DateTimeOffset.Parse("2026-08-16T12:00:00Z"),
                LocationDefaults.DeviceGps,
                "  ",
                LocationDefaults.ConsentVersion)
        };
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(null));
        MockProfileRepository
            .UpsertAsync(Arg.Any<Profile>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Profile>(0)));

        // Act
        var result = await Sut.UpdateOwnAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Location!.Visibility.Should().Be(LocationDefaults.Private);
        await MockProfileRepository.Received(1).UpsertAsync(
            Arg.Is<Profile>(profile => profile.LocationVisibility == LocationDefaults.Private),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistPublicVisibilityWhenTheUserChoseToShare()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var args = new UpdateProfileArgs
        {
            UserId = userId,
            DisplayName = "Eamonn",
            Location = new CatchLocationDto(
                53.4,
                -7.9,
                12,
                DateTimeOffset.Parse("2026-08-16T12:00:00Z"),
                LocationDefaults.DeviceGps,
                LocationDefaults.Public,
                LocationDefaults.ConsentVersion)
        };
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(null));
        MockProfileRepository
            .UpsertAsync(Arg.Any<Profile>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Profile>(0)));

        // Act
        var result = await Sut.UpdateOwnAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Location!.Visibility.Should().Be(LocationDefaults.Public);
        await MockProfileRepository.Received(1).UpsertAsync(
            Arg.Is<Profile>(profile => profile.LocationVisibility == LocationDefaults.Public),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistProfileFieldsAndKeepCapturedLocationPrivateByDefault()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var capturedOn = DateTimeOffset.Parse("2026-08-16T12:00:00Z");
        var args = new UpdateProfileArgs
        {
            UserId = userId,
            DisplayName = "Eamonn",
            HomeRegion = "Westmeath",
            PreferredFishingTypes = ["Coarse", "Fly"],
            PreferredSpecies = ["Pike", "Tench"],
            ShowDisplayName = true,
            ShowHomeRegion = true,
            ShowPreferredFishingTypes = true,
            ShowPreferredSpecies = false,
            Location = new CatchLocationDto(
                53.4,
                -7.9,
                12,
                capturedOn,
                LocationDefaults.DeviceGps,
                LocationDefaults.Private,
                LocationDefaults.ConsentVersion)
        };
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(null));
        MockProfileRepository
            .UpsertAsync(Arg.Any<Profile>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Profile>(0)));

        // Act
        var result = await Sut.UpdateOwnAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayName.Should().Be("Eamonn");
        result.Value.HomeRegion.Should().Be("Westmeath");
        result.Value.PreferredFishingTypes.Should().Equal("Coarse", "Fly");
        result.Value.PreferredSpecies.Should().Equal("Pike", "Tench");
        result.Value.Location!.Visibility.Should().Be(LocationDefaults.Private);
        await MockProfileRepository.Received(1).UpsertAsync(
            Arg.Is<Profile>(profile =>
                profile.DisplayName == "Eamonn"
                && profile.HomeRegion == "Westmeath"
                && profile.Latitude == 53.4
                && profile.Longitude == -7.9
                && profile.LocationVisibility == LocationDefaults.Private),
            Arg.Any<CancellationToken>());
    }
}
