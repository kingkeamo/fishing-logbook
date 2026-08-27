using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Trips.Components.TripEditor;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripEditorTests;

public class WhenTestingRender : BaseTripEditorTest
{
    [Fact]
    public async Task ItShouldLoadTheExistingTitleAndPlaceName()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var trip = ActiveTrip(title: "Morning session", placeName: "Lough Corrib");

        // Act
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, trip));

        // Assert
        cut.Find("#trip-editor-title").GetAttribute("value").Should().Be("Morning session");
        cut.Find("#trip-location-other").Should().NotBeNull();
        cut.Markup.Should().Contain("Lough Corrib");
    }

    [Fact]
    public async Task ItShouldPreserveAPlaceNameThatIsNotASavedPreference()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var preferences = QuietAnglerPreferences();
        await using var context = CreateContext(anglerPreferences: preferences);
        var trip = ActiveTrip(placeName: "A quiet spot behind the mill");

        // Act
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, trip));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-location-other").GetAttribute("value")
                .Should().Be("A quiet spot behind the mill"));
    }

    [Fact]
    public async Task ItShouldShowTheSummaryLabelWhenProvided()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, ActiveTrip())
            .Add(component => component.SummaryLabel, "27 Aug 2026 · 13:02"));

        // Assert
        cut.Find("#trip-editor-summary").TextContent.Should().Contain("27 Aug 2026 · 13:02");
    }

    [Fact]
    public async Task ItShouldNotRenderATimeline()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, ActiveTrip()));

        // Assert
        cut.FindAll("#trip-timeline").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldSayThereAreNoCatchesYet()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, ActiveTrip()));

        // Assert
        cut.Find("#trip-editor-catches-empty").TextContent.Should().Contain("No catches yet");
    }

    [Fact]
    public async Task ItShouldShowAssociatedCatchesWithSpeciesAndMeasurements()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var associated = AssociatedCatch(weight: 1.02m, length: 48m);

        // Act
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, ActiveTrip())
            .Add(component => component.Catches, new[] { associated }));

        // Assert
        var row = cut.Find($"#trip-editor-catch-{associated.Id:D}");
        row.TextContent.Should().Contain("Brown Trout");
        cut.Find($"#trip-editor-catch-measurements-{associated.Id:D}").TextContent
            .Should().Contain("48 cm");
    }

    [Fact]
    public async Task ItShouldOfferToRemoveACatchFromTheTripRatherThanDeleteIt()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var associated = AssociatedCatch();

        // Act
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, ActiveTrip())
            .Add(component => component.Catches, new[] { associated }));

        // Assert
        var removeButton = cut.Find($"#trip-editor-catch-remove-{associated.Id:D}");
        removeButton.GetAttribute("aria-label").Should().Contain("Remove Brown Trout from this trip");
        removeButton.GetAttribute("aria-label").Should().NotContain("Delete");
        removeButton.TextContent.Should().NotContain("Delete");
    }
}
