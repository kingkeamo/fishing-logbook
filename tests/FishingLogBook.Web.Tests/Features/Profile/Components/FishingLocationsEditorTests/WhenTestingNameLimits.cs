using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Profile.Components.FishingLocationsEditor;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components.Web;

namespace FishingLogBook.Web.Tests.Features.Profile.Components.FishingLocationsEditorTests;

public class WhenTestingNameLimits : BaseFishingLocationsEditorTest
{
    [Fact]
    public async Task ItShouldRejectANameOneCharacterOverTheLimit()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var locations = new List<FishingLocationEditModel>();
        var changes = 0;
        var cut = context.Render<FishingLocationsEditor>(parameters => parameters
            .Add(component => component.Locations, locations)
            .Add(component => component.OnChanged, () => changes++));
        cut.Find("#fishing-location-new-name").Input(new string('x', FishingLocationConstants.MaxNameLength + 1));

        // Act
        cut.Find("#fishing-location-add").Click();

        // Assert
        locations.Should().BeEmpty();
        changes.Should().Be(0);
        cut.Find("#fishing-location-new-name").GetAttribute("aria-invalid").Should().Be("true");
        cut.Markup.Should().Contain($"{FishingLocationConstants.MaxNameLength} characters or fewer");
    }

    [Fact]
    public async Task ItShouldAcceptANameExactlyAtTheLimit()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var locations = new List<FishingLocationEditModel>();
        var changes = 0;
        var name = new string('x', FishingLocationConstants.MaxNameLength);
        var cut = context.Render<FishingLocationsEditor>(parameters => parameters
            .Add(component => component.Locations, locations)
            .Add(component => component.OnChanged, () => changes++));
        cut.Find("#fishing-location-new-name").Input(name);

        // Act
        cut.Find("#fishing-location-add").Click();

        // Assert
        locations.Should().ContainSingle().Which.Name.Should().Be(name);
        changes.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldAddTheLocationWhenTheAnglerPressesNumpadEnter()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var locations = new List<FishingLocationEditModel>();
        var cut = context.Render<FishingLocationsEditor>(parameters => parameters
            .Add(component => component.Locations, locations));
        cut.Find("#fishing-location-new-name").Input("Lough Mask");

        // Act
        await cut.Find("#fishing-location-new-name").KeyDownAsync(new KeyboardEventArgs { Key = "NumpadEnter" });

        // Assert
        locations.Should().ContainSingle().Which.Name.Should().Be("Lough Mask");
    }

    [Fact]
    public async Task ItShouldIgnoreAKeyThatIsNotEnter()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var locations = new List<FishingLocationEditModel>();
        var cut = context.Render<FishingLocationsEditor>(parameters => parameters
            .Add(component => component.Locations, locations));
        cut.Find("#fishing-location-new-name").Input("Lough Mask");

        // Act
        await cut.Find("#fishing-location-new-name").KeyDownAsync(new KeyboardEventArgs { Key = "a" });

        // Assert
        locations.Should().BeEmpty();
    }
}
