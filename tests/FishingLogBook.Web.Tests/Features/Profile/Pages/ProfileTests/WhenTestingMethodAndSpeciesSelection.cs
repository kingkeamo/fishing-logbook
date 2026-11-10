using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Localization;
using NSubstitute;
using ProfilePage = FishingLogBook.Web.Features.Profile.Pages.Profile.Profile;

namespace FishingLogBook.Web.Tests.Features.Profile.Pages.ProfileTests;

public class WhenTestingMethodAndSpeciesSelection : BaseProfileTest
{
    private static FishingPreferencesDto FlyWithTwoSpecies()
    {
        return new FishingPreferencesDto(
        [
            new FishingMethodPreferenceDto(
                FlyMethodId,
                "Fly",
                "Fly",
                true,
                [
                    new FishingSpeciesPreferenceDto(BrownTroutSpeciesId, "BrownTrout", "Brown Trout", true),
                    new FishingSpeciesPreferenceDto(PikeSpeciesId, "Pike", "Pike", false)
                ])
        ]);
    }

    private static IProfileClient OwnProfileClient()
    {
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(EmptyProfile());
        return profileClient;
    }

    private static async Task<IRenderedComponent<ProfilePage>> RenderAsync(
        BunitContext context)
    {
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-method-Fly"));
        await Task.CompletedTask;
        return cut;
    }

    [Fact]
    public async Task ItShouldKeepTheSelectionWhenTheMethodAndSpeciesPickersAreDismissed()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var modal = QuietModalService();
        modal.ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Any<CataloguePickerModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CataloguePickerModalResult?>(null));
        await using var context = CreateContext(
            OwnProfileClient(),
            QuietFishingPreferenceClient(FlyWithTwoSpecies(), SampleCatalogue()),
            modal);
        var cut = await RenderAsync(context);

        // Act
        await cut.Find("#profile-method-more").ClickAsync();
        await cut.Find("#profile-species-more-Fly").ClickAsync();

        // Assert
        cut.Find("#profile-method-Fly").ClassList.Should().Contain("mud-chip-filled");
        cut.Find("#profile-species-pill-Fly-BrownTrout").Should().NotBeNull();
        cut.Find("#profile-species-pill-Fly-Pike").Should().NotBeNull();
        await modal.Received(2).ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
            Arg.Any<CataloguePickerModalModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMoveTheDefaultSpeciesWhenAnotherSpeciesIsChosenAsTheDefault()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(
            OwnProfileClient(),
            QuietFishingPreferenceClient(FlyWithTwoSpecies(), SampleCatalogue()));
        var cut = await RenderAsync(context);
        cut.Find("#profile-species-Fly-BrownTrout").ClassList.Should().Contain("mud-chip-color-secondary");

        // Act
        await cut.Find("#profile-species-Fly-Pike").ClickAsync();

        // Assert
        cut.Find("#profile-species-Fly-Pike").ClassList.Should().Contain("mud-chip-color-secondary");
        cut.Find("#profile-species-Fly-BrownTrout").ClassList.Should().NotContain("mud-chip-color-secondary");
    }

    [Fact]
    public async Task ItShouldPassTheDefaultToTheRemainingSpeciesWhenTheDefaultOneIsRemoved()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(
            OwnProfileClient(),
            QuietFishingPreferenceClient(FlyWithTwoSpecies(), SampleCatalogue()));
        var cut = await RenderAsync(context);

        // Act
        await cut.Find("#profile-species-remove-Fly-BrownTrout").ClickAsync();

        // Assert
        cut.FindAll("#profile-species-pill-Fly-BrownTrout").Should().BeEmpty();
        cut.Find("#profile-species-Fly-Pike").ClassList.Should().Contain("mud-chip-color-secondary");
    }

    [Fact]
    public async Task ItShouldMoveTheDefaultMethodWhenAnotherSelectedMethodIsChosenAndWhenTheDefaultIsDeselected()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(
            OwnProfileClient(),
            QuietFishingPreferenceClient(FlyWithTwoSpecies(), SampleCatalogue()));
        var cut = await RenderAsync(context);
        await cut.Find("#profile-method-Spinning").ClickAsync();
        cut.Find("#profile-method-default-Fly").ClassList.Should().Contain("mud-chip-color-secondary");

        // Act
        await cut.Find("#profile-method-default-Spinning").ClickAsync();

        // Assert
        cut.Find("#profile-method-default-Spinning").ClassList.Should().Contain("mud-chip-color-secondary");
        cut.Find("#profile-method-default-Fly").ClassList.Should().NotContain("mud-chip-color-secondary");

        // Act
        await cut.Find("#profile-method-Spinning").ClickAsync();

        // Assert
        cut.Find("#profile-method-Fly").ClassList.Should().Contain("mud-chip-filled");
        cut.FindAll("#profile-method-default-Spinning").Should().BeEmpty();
        cut.Find("#profile-method-default-Fly").ClassList.Should().Contain("mud-chip-color-secondary");
    }
}
