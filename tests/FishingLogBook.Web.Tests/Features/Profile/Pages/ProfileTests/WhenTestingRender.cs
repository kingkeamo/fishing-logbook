using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Features.Profile.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;
using ProfilePage = FishingLogBook.Web.Features.Profile.Pages.Profile.Profile;

namespace FishingLogBook.Web.Tests.Features.Profile.Pages.ProfileTests;

public class WhenTestingRender : BaseProfileTest
{
    [Fact]
    public async Task ItShouldShowEnglishProfileCopyAndADisabledShareSwitch()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(EmptyProfile());
        var locationService = Substitute.For<ILocationService>();
        locationService.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new LocationPromptStatus(true, false, false));
        await using var context = CreateContext(profileClient, locationService);

        // Act
        var cut = context.Render<ProfilePage>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Your profile");
            cut.Find("#profile-location-explainer").TextContent.Should()
                .Contain("Capturing it does not make it public");
            cut.Markup.Should().Contain("Share precise location with other anglers");
            cut.Find("#profile-share-location").HasAttribute("disabled").Should().BeTrue();
        });
        await profileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
        await locationService.DidNotReceive().TryCaptureAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchProfileCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(EmptyProfile());
        var locationService = Substitute.For<ILocationService>();
        locationService.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new LocationPromptStatus(true, false, false));
        await using var context = CreateContext(profileClient, locationService);

        // Act
        var cut = context.Render<ProfilePage>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Votre profil");
            cut.Find("#profile-location-allow").TextContent.Should().Contain("Autoriser la localisation");
            cut.Find("#profile-save-button").TextContent.Should().Contain("Enregistrer le profil");
        });
        await profileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
    }
}
