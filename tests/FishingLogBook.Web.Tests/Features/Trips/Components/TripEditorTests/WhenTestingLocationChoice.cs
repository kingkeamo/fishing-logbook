using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Trips.Components.TripEditor;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripEditorTests;

public class WhenTestingLocationChoice : BaseTripEditorTest
{
    [Fact]
    public async Task ItShouldShowTheSavedLocationMatchingTheTripAsSelected()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(anglerPreferences: QuietAnglerPreferences(Corrib(), Moy()));

        // Act
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, ActiveTrip(placeName: "Lough Corrib")));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-location-choice-lough-corrib").GetAttribute("aria-pressed").Should().Be("true"));
        cut.Find("#trip-location-choice-river-moy").GetAttribute("aria-pressed").Should().Be("false");
    }

    [Fact]
    public async Task ItShouldSaveAnotherSavedLocationChosenFromTheQuickChoices()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = TripServiceThatSaves();
        await using var context = CreateContext(
            activeTrip: activeTrip,
            anglerPreferences: QuietAnglerPreferences(Corrib(), Moy()));
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, ActiveTrip("Morning session", "Lough Corrib")));
        cut.WaitForAssertion(() => cut.Find("#trip-location-choice-river-moy").Should().NotBeNull());

        // Act
        cut.Find("#trip-location-choice-river-moy").Click();
        await cut.Find("#trip-editor-save").ClickAsync();

        // Assert
        await activeTrip.Received(1).UpdateDetailsAsync(
            Arg.Is<TripModel>(saved => saved.Id == TripId),
            "Morning session",
            "River Moy",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotChangeTheSavedLocationsWhenTheTripPlaceIsEdited()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var preferences = QuietAnglerPreferences(Corrib(), Moy());
        var activeTrip = TripServiceThatSaves();
        await using var context = CreateContext(activeTrip: activeTrip, anglerPreferences: preferences);
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, ActiveTrip(placeName: "Lough Corrib")));
        cut.WaitForAssertion(() => cut.Find("#trip-location-choice-river-moy").Should().NotBeNull());

        // Act
        cut.Find("#trip-location-choice-river-moy").Click();
        await cut.Find("#trip-editor-save").ClickAsync();

        // Assert
        await preferences.DidNotReceive().SetAsync(
            Arg.Any<Guid>(),
            Arg.Any<Web.Features.Profile.Models.AnglerPreferencesModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSayThatSaveOnlyAppliesToTheTripFields()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, ActiveTrip("Morning session")));

        // Assert
        cut.Find("#trip-editor-save-scope").TextContent.Should()
            .Contain("Photos, notes and catches are saved as you add or remove them.");
    }

    [Fact]
    public async Task ItShouldShowFrenchEditorCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, ActiveTrip("Morning session")));

        // Assert
        cut.Find("#trip-editor-heading").TextContent.Should().Contain("Modifier la sortie");
        cut.Find("#trip-editor-save").TextContent.Should().Contain("Enregistrer");
    }
}
