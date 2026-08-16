using AwesomeAssertions;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Tests.Common.Builders;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Services.ProfileServiceTests;

public class WhenTestingGetPublic : BaseProfileServiceTest
{
    [Fact]
    public async Task ItShouldOmitHiddenPersonalInformation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var profile = new ProfileBuilder()
            .WithUserId(userId)
            .WithDisplayName("Eamonn")
            .WithHomeRegion("Westmeath")
            .WithFishingTypes("Fly")
            .WithSpecies("Pike")
            .HideAll()
            .WithLocation(new CatchLocationDto(
                53.4,
                -7.9,
                8,
                DateTimeOffset.Parse("2026-08-16T12:00:00Z"),
                LocationDefaults.DeviceGps,
                LocationDefaults.Private,
                LocationDefaults.ConsentVersion))
            .Build();
        MockProfileRepository
            .UserExistsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(true));
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(profile));

        // Act
        var result = await Sut.GetPublicAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayName.Should().BeNull();
        result.Value.HomeRegion.Should().BeNull();
        result.Value.PhotographUrl.Should().BeNull();
        result.Value.PreferredFishingTypes.Should().BeEmpty();
        result.Value.PreferredSpecies.Should().BeEmpty();
        result.Value.Location.Should().BeNull();
        typeof(PublicProfileDto).GetProperty("Latitude").Should().BeNull();
        typeof(PublicProfileDto).GetProperty("Longitude").Should().BeNull();
        typeof(PublicProfileDto).GetProperty("Email").Should().BeNull();
        await MockProfileRepository.Received(1).UserExistsAsync(userId, Arg.Any<CancellationToken>());
        await MockProfileRepository.Received(1).GetByUserIdAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepPreciseCoordinatesPrivateWhenTheUserHasNotChosenToShareThem()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var profile = new ProfileBuilder()
            .WithUserId(userId)
            .ShowAll()
            .WithLocation(new CatchLocationDto(
                53.4,
                -7.9,
                8,
                DateTimeOffset.Parse("2026-08-16T12:00:00Z"),
                LocationDefaults.DeviceGps,
                LocationDefaults.Private,
                LocationDefaults.ConsentVersion))
            .Build();
        MockProfileRepository
            .UserExistsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(true));
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(profile));

        // Act
        var result = await Sut.GetPublicAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Location.Should().BeNull();
        await MockProfileRepository.Received(1).GetByUserIdAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIncludePreciseCoordinatesOnlyWhenVisibilityIsPublic()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var profile = new ProfileBuilder()
            .WithUserId(userId)
            .ShowAll()
            .WithLocation(new CatchLocationDto(
                53.4,
                -7.9,
                8,
                DateTimeOffset.Parse("2026-08-16T12:00:00Z"),
                LocationDefaults.DeviceGps,
                LocationDefaults.Public,
                LocationDefaults.ConsentVersion))
            .Build();
        MockProfileRepository
            .UserExistsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(true));
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(profile));

        // Act
        var result = await Sut.GetPublicAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Location.Should().NotBeNull();
        result.Value.Location!.Latitude.Should().Be(53.4);
        result.Value.Location.Longitude.Should().Be(-7.9);
        await MockProfileRepository.Received(1).GetByUserIdAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWhenTheUserDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        MockProfileRepository
            .UserExistsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(false));

        // Act
        var result = await Sut.GetPublicAsync(userId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Angler profile was not found.");
        await MockProfileRepository.DidNotReceive().GetByUserIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }
}
