using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Profile.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;
using ProfilePage = FishingLogBook.Web.Features.Profile.Pages.Profile.Profile;

namespace FishingLogBook.Web.Tests.Features.Profile.Pages.ProfileTests;

public class WhenTestingRender : BaseProfileTest
{
    [Fact]
    public async Task ItShouldShowEnglishProfileCopyWithoutPreciseLocationControls()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(EmptyProfile());
        await using var context = CreateContext(profileClient);

        // Act
        var cut = context.Render<ProfilePage>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Your profile");
            cut.Markup.Should().Contain("Choose what other anglers can see.");
            cut.Find("#profile-display-name").Should().NotBeNull();
            cut.Find("#profile-home-region").Should().NotBeNull();
            cut.Find("#profile-fishing-types").Should().NotBeNull();
            cut.Find("#profile-preferred-species").Should().NotBeNull();
            cut.Find("#profile-show-display-name").Should().NotBeNull();
            cut.Find("#profile-show-photograph").Should().NotBeNull();
            cut.Find("#profile-location-privacy").TextContent.Should()
                .Contain("Enabling device location or joining a club does not share your precise coordinates.");
            cut.FindAll("#profile-location-allow").Should().BeEmpty();
            cut.FindAll("#profile-share-location").Should().BeEmpty();
            cut.Markup.Should().NotContain("Share precise location");
        });
        await profileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchProfileCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(EmptyProfile());
        await using var context = CreateContext(profileClient);

        // Act
        var cut = context.Render<ProfilePage>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Votre profil");
            cut.Markup.Should().Contain("Choisissez ce que les autres pêcheurs peuvent voir.");
            cut.Find("#profile-save-button").TextContent.Should().Contain("Enregistrer le profil");
            cut.Find("#profile-location-privacy").TextContent.Should()
                .Contain("Activer la localisation");
        });
        await profileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
    }
}
