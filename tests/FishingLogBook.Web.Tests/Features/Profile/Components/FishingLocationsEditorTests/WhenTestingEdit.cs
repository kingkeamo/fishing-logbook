using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Profile.Components.FishingLocationsEditor;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;

namespace FishingLogBook.Web.Tests.Features.Profile.Components.FishingLocationsEditorTests;

public class WhenTestingEdit : BaseFishingLocationsEditorTest
{
    [Fact]
    public async Task ItShouldSayWhenNoLocationsAreSaved()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var locations = new List<FishingLocationEditModel>();

        // Act
        var cut = context.Render<FishingLocationsEditor>(parameters => parameters
            .Add(component => component.Locations, locations));

        // Assert
        cut.Find("#fishing-locations-empty").TextContent.Should().Contain("No saved fishing locations");
        cut.FindAll("#fishing-locations-list").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotAddABlankLocation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var locations = new List<FishingLocationEditModel>();
        var changes = 0;
        var cut = context.Render<FishingLocationsEditor>(parameters => parameters
            .Add(component => component.Locations, locations)
            .Add(component => component.OnChanged, () => changes++));
        cut.Find("#fishing-location-new-name").Input("   ");

        // Act
        cut.Find("#fishing-location-add").Click();

        // Assert
        locations.Should().BeEmpty();
        changes.Should().Be(0);
        cut.Find("#fishing-location-new-name").GetAttribute("aria-invalid").Should().Be("true");
    }

    [Fact]
    public async Task ItShouldNotAddADuplicateLocationIgnoringCaseAndSpacing()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var locations = SavedLocations();
        var changes = 0;
        var cut = context.Render<FishingLocationsEditor>(parameters => parameters
            .Add(component => component.Locations, locations)
            .Add(component => component.OnChanged, () => changes++));
        cut.Find("#fishing-location-new-name").Input("  lough corrib ");

        // Act
        cut.Find("#fishing-location-add").Click();

        // Assert
        locations.Should().HaveCount(2);
        changes.Should().Be(0);
        cut.Markup.Should().Contain("That fishing location is already saved.");
    }

    [Fact]
    public async Task ItShouldRemoveALocationWithoutPromotingAnotherDefault()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var locations = SavedLocations();
        var changes = 0;
        var cut = context.Render<FishingLocationsEditor>(parameters => parameters
            .Add(component => component.Locations, locations)
            .Add(component => component.OnChanged, () => changes++));

        // Act
        cut.Find("#fishing-location-lough-corrib-remove").Click();

        // Assert
        locations.Select(location => location.Name).Should().Equal("River Moy");
        locations.Should().OnlyContain(location => !location.IsDefault);
        changes.Should().Be(1);
        cut.FindAll("#fishing-location-river-moy-default").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldMoveTheDefaultToTheChosenLocation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var locations = SavedLocations();
        var changes = 0;
        var cut = context.Render<FishingLocationsEditor>(parameters => parameters
            .Add(component => component.Locations, locations)
            .Add(component => component.OnChanged, () => changes++));

        // Act
        cut.Find("#fishing-location-river-moy-set-default").Click();

        // Assert
        locations.Single(location => location.IsDefault).Name.Should().Be("River Moy");
        locations.Count(location => location.IsDefault).Should().Be(1);
        changes.Should().Be(1);
        cut.Find("#fishing-location-river-moy-default").TextContent.Should().Contain("Default");
        cut.FindAll("#fishing-location-lough-corrib-default").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldAddATrimmedLocationWithoutMakingItTheDefault()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var locations = SavedLocations();
        var changes = 0;
        var cut = context.Render<FishingLocationsEditor>(parameters => parameters
            .Add(component => component.Locations, locations)
            .Add(component => component.OnChanged, () => changes++));
        cut.Find("#fishing-location-new-name").Input("  Lough Mask  ");

        // Act
        cut.Find("#fishing-location-add").Click();

        // Assert
        locations.Select(location => location.Name)
            .Should().Equal("Lough Corrib", "River Moy", "Lough Mask");
        locations.Last().IsDefault.Should().BeFalse();
        locations.Last().Id.Should().Be(Guid.Empty);
        changes.Should().Be(1);
        cut.Find("#fishing-location-lough-mask").TextContent.Should().Contain("Lough Mask");
    }

    [Fact]
    public async Task ItShouldShowFrenchCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        await using var context = CreateContext();
        var locations = SavedLocations();

        // Act
        var cut = context.Render<FishingLocationsEditor>(parameters => parameters
            .Add(component => component.Locations, locations));

        // Assert
        cut.Markup.Should().Contain("Lieux de pêche");
        cut.Find("#fishing-location-lough-corrib-default").TextContent.Should().Contain("Par défaut");
        cut.Find("#fishing-location-river-moy-set-default").TextContent.Should().Contain("Définir par défaut");
    }

    [Fact]
    public async Task ItShouldLabelTheAccessibleActionsWithTheLocationName()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var locations = SavedLocations();

        // Act
        var cut = context.Render<FishingLocationsEditor>(parameters => parameters
            .Add(component => component.Locations, locations));

        // Assert
        cut.Find("#fishing-location-lough-corrib-remove").GetAttribute("aria-label")
            .Should().Be("Remove the fishing location Lough Corrib");
        cut.Find("#fishing-location-river-moy-set-default").GetAttribute("aria-label")
            .Should().Be("Set River Moy as the default fishing location");
    }
}
