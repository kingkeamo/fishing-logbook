using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Profile.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;
using ProfilePage = FishingLogBook.Web.Features.Profile.Pages.Profile.Profile;

namespace FishingLogBook.Web.Tests.Features.Profile.Pages.ProfileTests;

public class WhenTestingRender : BaseProfileTest
{
    [Fact]
    public async Task ItShouldShowLoadingUntilTheProfileIsLoaded()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var loadStarted = new TaskCompletionSource();
        var loadContinue = new TaskCompletionSource<ProfileDto>();
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                loadStarted.TrySetResult();
                return await loadContinue.Task;
            });
        await using var context = CreateContext(profileClient);

        // Act
        var cut = context.Render<ProfilePage>();
        await loadStarted.Task;

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#profile-loading").Should().NotBeNull();
            cut.FindAll("#profile-display-name").Should().BeEmpty();
            cut.FindAll("#profile-save-button").Should().BeEmpty();
        });
        loadContinue.SetResult(EmptyProfile());
        cut.WaitForAssertion(() =>
        {
            cut.Find("#profile-display-name").Should().NotBeNull();
            cut.FindAll("#profile-loading").Should().BeEmpty();
        });
        await profileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPopulateTheFormFromASuccessfulLoad()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(new ProfileDto(
                userId,
                "Eamonn",
                Guid.NewGuid(),
                "https://storage.test/photo",
                "image/jpeg",
                "Westmeath",
                ["Fly"],
                ["Pike", "Tench"],
                true,
                true,
                true,
                true,
                false));
        await using var context = CreateContext(profileClient);

        // Act
        var cut = context.Render<ProfilePage>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#profile-display-name").GetAttribute("value").Should().Be("Eamonn");
            cut.Find("#profile-home-region").GetAttribute("value").Should().Be("Westmeath");
            cut.Find("#profile-preferred-species").GetAttribute("value").Should().Be("Pike, Tench");
            cut.Find("#profile-photo-preview").GetAttribute("src").Should().Be("https://storage.test/photo");
            cut.FindAll("#profile-loading").Should().BeEmpty();
            cut.FindAll("#profile-load-failed").Should().BeEmpty();
        });
        await profileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
    }

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
            cut.Find("#profile-privacy-caption").TextContent.Should()
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
            cut.Find("#profile-privacy-caption").TextContent.Should()
                .Contain("Activer la localisation");
        });
        await profileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
    }
}
