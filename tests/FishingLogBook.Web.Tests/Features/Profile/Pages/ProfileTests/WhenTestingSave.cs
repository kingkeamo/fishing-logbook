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

public class WhenTestingSave : BaseProfileTest
{
    [Fact]
    public async Task ItShouldSaveDisplayNameHomeRegionPreferencesAndPrivateLocation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var capturedOn = DateTimeOffset.Parse("2026-08-16T12:00:00Z");
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(new ProfileDto(
                userId,
                null,
                null,
                null,
                null,
                null,
                ["Coarse"],
                [],
                true,
                false,
                false,
                false,
                false));
        profileClient.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ToSaved(userId, call.ArgAt<UpdateProfileDto>(0)));
        var locationService = Substitute.For<ILocationService>();
        locationService.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new LocationPromptStatus(true, false, false));
        locationService.TryCaptureAsync(true, Arg.Any<CancellationToken>())
            .Returns(new TestCatchLocationModel(
                53.4,
                -7.9,
                12,
                capturedOn,
                LocationDefaults.DeviceGps,
                LocationDefaults.Private,
                LocationDefaults.ConsentVersion));
        await using var context = CreateContext(profileClient, locationService);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-display-name"));

        // Act
        cut.Find("#profile-display-name").Input("Eamonn");
        cut.Find("#profile-home-region").Input("Westmeath");
        cut.Find("#profile-preferred-species").Input("Pike, Tench");
        await cut.Find("#profile-location-allow").ClickAsync();
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#profile-save-failed").Should().BeEmpty());
        await profileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
        await locationService.Received(1).TryCaptureAsync(true, Arg.Any<CancellationToken>());
        await profileClient.Received(1).UpdateOwnAsync(
            Arg.Is<UpdateProfileDto>(profile =>
                profile.DisplayName == "Eamonn"
                && profile.HomeRegion == "Westmeath"
                && profile.PreferredSpecies.SequenceEqual(new[] { "Pike", "Tench" })
                && profile.PreferredFishingTypes.SequenceEqual(new[] { "Coarse" })
                && profile.Location != null
                && profile.Location.Latitude == 53.4
                && profile.Location.Longitude == -7.9
                && profile.Location.Visibility == LocationDefaults.Private),
            Arg.Any<CancellationToken>());
        await profileClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
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
