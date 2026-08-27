using AngleSharp.Html.Dom;
using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Features.Trips.Components.TripLocationPicker;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripLocationPickerTests;

public class WhenTestingSelect : BaseTripLocationPickerTest
{
    [Fact]
    public async Task ItShouldStillOfferManualEntryWhenNoLocationsAreSaved()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var chosen = new List<string?>();

        // Act
        var cut = context.Render<TripLocationPicker>(parameters => parameters
            .Add(component => component.PlaceNameChanged, place => chosen.Add(place)));

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#trip-location-choices").Should().BeEmpty());
        cut.Find("#trip-location-other").Should().NotBeNull();
        cut.FindAll("#trip-location-clear").Should().BeEmpty();
        chosen.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldStillRenderTheCurrentPlaceWhenReadingThePreferencesFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var preferences = Substitute.For<IAnglerPreferencesProvider>();
        preferences.GetAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("preferences unavailable"));
        var logging = QuietLogging();
        await using var context = CreateContext(preferences, logging);

        // Act
        var cut = context.Render<TripLocationPicker>(parameters => parameters
            .Add(component => component.PlaceName, "Small lake near Clifden"));

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#trip-location-choices").Should().BeEmpty());
        OtherLocationValue(cut).Should().Be("Small lake near Clifden");
        await logging.Received(1).LogErrorAsync(
            "loading saved fishing locations",
            Arg.Any<InvalidOperationException>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPreserveAPlaceThatIsNotSavedAsAPreference()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(PreferencesWith(Corrib(), Moy()));
        var chosen = new List<string?>();

        // Act
        var cut = context.Render<TripLocationPicker>(parameters => parameters
            .Add(component => component.PlaceName, "Small lake near Clifden")
            .Add(component => component.PlaceNameChanged, place => chosen.Add(place)));

        // Assert
        cut.WaitForAssertion(() => OtherLocationValue(cut).Should().Be("Small lake near Clifden"));
        cut.Find("#trip-location-choice-lough-corrib").GetAttribute("aria-pressed").Should().Be("false");
        cut.Find("#trip-location-choice-river-moy").GetAttribute("aria-pressed").Should().Be("false");
        chosen.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowTheSavedLocationMatchingThePlaceAsSelected()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(PreferencesWith(Corrib(), Moy()));

        // Act
        var cut = context.Render<TripLocationPicker>(parameters => parameters
            .Add(component => component.PlaceName, "lough corrib"));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-location-choice-lough-corrib").GetAttribute("aria-pressed").Should().Be("true"));
        cut.Find("#trip-location-choice-river-moy").GetAttribute("aria-pressed").Should().Be("false");
        OtherLocationValue(cut).Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task ItShouldRaiseAClearedPlace()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(PreferencesWith(Corrib()));
        var chosen = new List<string?>();
        var cut = context.Render<TripLocationPicker>(parameters => parameters
            .Add(component => component.PlaceName, "Lough Corrib")
            .Add(component => component.PlaceNameChanged, place => chosen.Add(place)));
        cut.WaitForAssertion(() => cut.Find("#trip-location-clear").Should().NotBeNull());

        // Act
        cut.Find("#trip-location-clear").Click();

        // Assert
        chosen.Should().ContainSingle();
        chosen[0].Should().BeNull();
    }

    [Fact]
    public async Task ItShouldRaiseAManualPlace()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(PreferencesWith(Corrib()));
        var chosen = new List<string?>();
        var cut = context.Render<TripLocationPicker>(parameters => parameters
            .Add(component => component.PlaceName, "Lough Corrib")
            .Add(component => component.PlaceNameChanged, place => chosen.Add(place)));
        cut.WaitForAssertion(() => cut.Find("#trip-location-other").Should().NotBeNull());

        // Act
        cut.Find("#trip-location-other").Change("Small lake near Clifden");

        // Assert
        chosen.Should().ContainSingle();
        chosen[0].Should().Be("Small lake near Clifden");
        cut.Find("#trip-location-choice-lough-corrib").GetAttribute("aria-pressed").Should().Be("false");
    }

    [Fact]
    public async Task ItShouldRaiseAnotherSavedLocation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(PreferencesWith(Corrib(), Moy()));
        var chosen = new List<string?>();
        var cut = context.Render<TripLocationPicker>(parameters => parameters
            .Add(component => component.PlaceName, "Lough Corrib")
            .Add(component => component.PlaceNameChanged, place => chosen.Add(place)));
        cut.WaitForAssertion(() => cut.Find("#trip-location-choice-river-moy").Should().NotBeNull());

        // Act
        cut.Find("#trip-location-choice-river-moy").Click();

        // Assert
        chosen.Should().ContainSingle();
        chosen[0].Should().Be("River Moy");
        cut.Find("#trip-location-choice-river-moy").GetAttribute("aria-pressed").Should().Be("true");
        cut.Find("#trip-location-choice-lough-corrib").GetAttribute("aria-pressed").Should().Be("false");
    }

    [Fact]
    public async Task ItShouldNotRaiseAChangeWhenTheChosenLocationIsAlreadyThePlace()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(PreferencesWith(Corrib()));
        var chosen = new List<string?>();
        var cut = context.Render<TripLocationPicker>(parameters => parameters
            .Add(component => component.PlaceName, "Lough Corrib")
            .Add(component => component.PlaceNameChanged, place => chosen.Add(place)));
        cut.WaitForAssertion(() => cut.Find("#trip-location-choice-lough-corrib").Should().NotBeNull());

        // Act
        cut.Find("#trip-location-choice-lough-corrib").Click();

        // Assert
        chosen.Should().BeEmpty();
        cut.Find("#trip-location-choice-lough-corrib").GetAttribute("aria-pressed").Should().Be("true");
    }

    [Fact]
    public async Task ItShouldShowFrenchCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        await using var context = CreateContext(PreferencesWith(Corrib()));

        // Act
        var cut = context.Render<TripLocationPicker>(parameters => parameters
            .Add(component => component.PlaceName, "Lough Corrib"));

        // Assert
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Lieu de pêche"));
        cut.Markup.Should().Contain("Autre lieu");
        cut.Find("#trip-location-clear").TextContent.Should().Contain("Effacer le lieu");
    }

    private static string? OtherLocationValue(IRenderedComponent<TripLocationPicker> cut)
    {
        return ((IHtmlInputElement)cut.Find("#trip-location-other")).Value;
    }
}
