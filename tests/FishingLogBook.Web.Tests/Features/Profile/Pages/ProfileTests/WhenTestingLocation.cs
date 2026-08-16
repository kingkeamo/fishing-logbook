using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Features.Profile.Services;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Localization;
using NSubstitute;
using ProfilePage = FishingLogBook.Web.Features.Profile.Pages.Profile.Profile;

namespace FishingLogBook.Web.Tests.Features.Profile.Pages.ProfileTests;

public class WhenTestingLocation : BaseProfileTest
{
    [Fact]
    public async Task ItShouldKeepCapturedLocationPrivateUntilTheUserChoosesToShare()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(EmptyProfile(userId));
        profileClient.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ToSaved(userId, call.ArgAt<UpdateProfileDto>(0)));
        var locationService = Substitute.For<ILocationService>();
        locationService.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new LocationPromptStatus(true, false, false));
        locationService.TryCaptureAsync(true, Arg.Any<CancellationToken>())
            .Returns(CapturedLocation());
        await using var context = CreateContext(profileClient, locationService);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-location-allow"));

        // Act
        await cut.Find("#profile-location-allow").ClickAsync();
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#profile-location-saved").TextContent.Should().Contain("Location saved"));
        await locationService.Received(1).TryCaptureAsync(true, Arg.Any<CancellationToken>());
        await profileClient.Received(1).UpdateOwnAsync(
            Arg.Is<UpdateProfileDto>(profile =>
                profile.Location != null
                && profile.Location.Latitude == 53.4
                && profile.Location.Visibility == LocationDefaults.Private),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSharePreciseLocationOnlyWhenTheUserTurnsTheSwitchOn()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(EmptyProfile(userId));
        profileClient.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ToSaved(userId, call.ArgAt<UpdateProfileDto>(0)));
        var locationService = Substitute.For<ILocationService>();
        locationService.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new LocationPromptStatus(true, false, false));
        locationService.TryCaptureAsync(true, Arg.Any<CancellationToken>())
            .Returns(CapturedLocation());
        await using var context = CreateContext(profileClient, locationService);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-location-allow"));

        // Act
        await cut.Find("#profile-location-allow").ClickAsync();
        cut.WaitForAssertion(() => cut.Find("#profile-share-location").HasAttribute("disabled").Should().BeFalse());
        cut.Find("#profile-share-location").Change(true);
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        await profileClient.Received(1).UpdateOwnAsync(
            Arg.Is<UpdateProfileDto>(profile =>
                profile.Location != null
                && profile.Location.Latitude == 53.4
                && profile.Location.Visibility == LocationDefaults.Public),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTryAgainWhenLocationIsNotGranted()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(EmptyProfile());
        var locationService = Substitute.For<ILocationService>();
        locationService.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new LocationPromptStatus(false, true, false));
        await using var context = CreateContext(profileClient, locationService);

        // Act
        var cut = context.Render<ProfilePage>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("#profile-location-explainer").Should().BeEmpty();
            cut.Find("#profile-location-enable").TextContent.Should().Contain("Device location is off");
            cut.Find("#profile-location-try-again").TextContent.Should().Contain("Try location again");
        });
        await locationService.DidNotReceive().TryCaptureAsync(true, Arg.Any<CancellationToken>());
        await profileClient.DidNotReceive().UpdateOwnAsync(
            Arg.Any<UpdateProfileDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopShowingTheExplainerWhenNotNowIsChosen()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(EmptyProfile());
        var locationService = Substitute.For<ILocationService>();
        locationService.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(
                new LocationPromptStatus(true, false, false),
                new LocationPromptStatus(false, true, false));
        await using var context = CreateContext(profileClient, locationService);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-location-not-now"));

        // Act
        await cut.Find("#profile-location-not-now").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("#profile-location-explainer").Should().BeEmpty();
            cut.Find("#profile-location-enable").Should().NotBeNull();
        });
        await locationService.Received(1).DismissPromptAsync(Arg.Any<CancellationToken>());
        await locationService.DidNotReceive().TryCaptureAsync(true, Arg.Any<CancellationToken>());
    }

    private static TestCatchLocationModel CapturedLocation()
    {
        return new TestCatchLocationModel(
            53.4,
            -7.9,
            12,
            DateTimeOffset.Parse("2026-08-16T12:00:00Z"),
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
    }

    private static ProfileDto ToSaved(Guid userId, UpdateProfileDto update)
    {
        return new ProfileDto(
            userId,
            update.DisplayName,
            null,
            null,
            null,
            update.HomeRegion,
            update.PreferredFishingTypes,
            update.PreferredSpecies,
            update.ShowDisplayName,
            update.ShowPhotograph,
            update.ShowHomeRegion,
            update.ShowPreferredFishingTypes,
            update.ShowPreferredSpecies,
            update.Location);
    }
}
