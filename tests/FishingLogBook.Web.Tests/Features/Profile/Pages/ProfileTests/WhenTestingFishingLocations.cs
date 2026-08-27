using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;
using ProfilePage = FishingLogBook.Web.Features.Profile.Pages.Profile.Profile;

namespace FishingLogBook.Web.Tests.Features.Profile.Pages.ProfileTests;

public class WhenTestingFishingLocations : BaseProfileTest
{
    private static readonly Guid CorribId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid MoyId = Guid.Parse("dddddddd-0000-0000-0000-000000000002");

    [Fact]
    public async Task ItShouldShowSaveFailureWhenTheLocationsCannotBeSaved()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = QuietProfileClient(userId);
        var locationClient = QuietFishingLocationClient(SavedLocations());
        locationClient.UpdateAsync(
                Arg.Any<UpdateFishingLocationPreferencesDto>(),
                Arg.Any<CancellationToken>())
            .Returns<FishingLocationPreferencesDto>(_ => throw new HttpRequestException("rejected"));
        await using var context = CreateContext(profileClient, fishingLocationClient: locationClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-save-button"));

        // Act
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#profile-save-failed").Should().NotBeNull());
        await locationClient.Received(1).UpdateAsync(
            Arg.Any<UpdateFishingLocationPreferencesDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheSavedLocationsAndTheDefault()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = QuietProfileClient(Guid.NewGuid());
        var locationClient = QuietFishingLocationClient(SavedLocations());
        await using var context = CreateContext(profileClient, fishingLocationClient: locationClient);

        // Act
        var cut = context.Render<ProfilePage>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#fishing-location-lough-corrib").TextContent.Should().Contain("Lough Corrib"));
        cut.Find("#fishing-location-lough-corrib-default").TextContent.Should().Contain("Default");
        cut.Find("#fishing-location-river-moy").TextContent.Should().Contain("River Moy");
        cut.FindAll("#fishing-location-river-moy-default").Should().BeEmpty();
        await locationClient.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveAnAddedLocationWithoutTouchingTheHomeRegion()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = QuietProfileClient(userId);
        var locationClient = QuietFishingLocationClient(SavedLocations());
        await using var context = CreateContext(profileClient, fishingLocationClient: locationClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#fishing-location-new-name"));
        cut.Find("#fishing-location-new-name").Input("Lough Mask");
        cut.Find("#fishing-location-add").Click();

        // Act
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#profile-save-failed").Should().BeEmpty());
        await locationClient.Received(1).UpdateAsync(
            Arg.Is<UpdateFishingLocationPreferencesDto>(update =>
                update.Locations.Count == 3 &&
                update.Locations[0].Id == CorribId &&
                update.Locations[0].IsDefault &&
                update.Locations[2].Name == "Lough Mask" &&
                update.Locations[2].Id == Guid.Empty &&
                !update.Locations[2].IsDefault),
            Arg.Any<CancellationToken>());
        await profileClient.Received(1).UpdateOwnAsync(
            Arg.Is<UpdateProfileDto>(update => update.HomeRegion == "Connacht"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveTheRemovalOfTheDefaultLocationWithNoReplacement()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = QuietProfileClient(Guid.NewGuid());
        var locationClient = QuietFishingLocationClient(SavedLocations());
        await using var context = CreateContext(profileClient, fishingLocationClient: locationClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#fishing-location-lough-corrib-remove"));
        cut.Find("#fishing-location-lough-corrib-remove").Click();

        // Act
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        await locationClient.Received(1).UpdateAsync(
            Arg.Is<UpdateFishingLocationPreferencesDto>(update =>
                update.Locations.Count == 1 &&
                update.Locations[0].Id == MoyId &&
                !update.Locations[0].IsDefault),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCacheTheSavedLocationsForOfflineUse()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = QuietProfileClient(userId);
        var locationClient = QuietFishingLocationClient(SavedLocations());
        var anglerPreferences = Substitute.For<IAnglerPreferencesProvider>();
        await using var context = CreateContext(
            profileClient,
            anglerPreferences: anglerPreferences,
            fishingLocationClient: locationClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-save-button"));

        // Act
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#profile-save-failed").Should().BeEmpty());
        await anglerPreferences.Received(1).SetAsync(
            userId,
            Arg.Is<AnglerPreferencesModel>(preferences =>
                preferences.Locations.Count == 2 &&
                preferences.DefaultLocation != null &&
                preferences.DefaultLocation.Name == "Lough Corrib"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchLocationCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var profileClient = QuietProfileClient(Guid.NewGuid());
        var locationClient = QuietFishingLocationClient(SavedLocations());
        await using var context = CreateContext(profileClient, fishingLocationClient: locationClient);

        // Act
        var cut = context.Render<ProfilePage>();

        // Assert
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Lieux de pêche"));
        cut.Markup.Should().Contain("Ajouter un lieu de pêche");
        await locationClient.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    private static IProfileClient QuietProfileClient(Guid userId)
    {
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(LoadedProfile(userId));
        profileClient.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ToSaved(userId, call.ArgAt<UpdateProfileDto>(0)));
        return profileClient;
    }

    private static ProfileDto LoadedProfile(Guid userId)
    {
        return new ProfileDto(
            userId,
            "Eamonn",
            null,
            null,
            null,
            "Connacht",
            true,
            false,
            false,
            false,
            false);
    }

    private static FishingLocationPreferencesDto SavedLocations()
    {
        return new FishingLocationPreferencesDto(
        [
            new FishingLocationPreferenceDto(CorribId, "Lough Corrib", true),
            new FishingLocationPreferenceDto(MoyId, "River Moy", false)
        ]);
    }
}
