using AngleSharp.Html.Dom;
using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Trips.Components.TripLocationPicker;
using FishingLogBook.Web.Features.Trips.Models;
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
        var activeTrip = TripServiceThatSaves();
        await using var context = CreateContext(activeTrip);

        // Act
        var cut = context.Render<TripLocationPicker>(parameters => parameters
            .Add(component => component.Trip, Trip()));

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#trip-location-choices").Should().BeEmpty());
        cut.Find("#trip-location-other").Should().NotBeNull();
        cut.FindAll("#trip-location-clear").Should().BeEmpty();
        await activeTrip.DidNotReceive().UpdatePlaceAsync(
            Arg.Any<TripModel>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheFailureWhenTheTripCanNoLongerBeSaved()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = Substitute.For<Web.Features.Trips.Services.IActiveTripService>();
        activeTrip.UpdatePlaceAsync(Arg.Any<TripModel>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((TripModel?)null);
        await using var context = CreateContext(activeTrip, PreferencesWith(Corrib(false)));
        var cut = context.Render<TripLocationPicker>(parameters => parameters
            .Add(component => component.Trip, Trip()));
        cut.WaitForAssertion(() => cut.Find("#trip-location-choice-lough-corrib").Should().NotBeNull());

        // Act
        cut.Find("#trip-location-choice-lough-corrib").Click();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-location-failed").TextContent.Should()
                .Contain("The fishing location could not be saved."));
        await activeTrip.Received(1).UpdatePlaceAsync(
            Arg.Any<TripModel>(),
            "Lough Corrib",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillRenderTheChoicesWhenReadingThePreferencesFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = TripServiceThatSaves();
        var preferences = Substitute.For<Web.Features.Profile.Providers.IAnglerPreferencesProvider>();
        preferences.GetAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("preferences unavailable"));
        var logging = QuietLogging();
        await using var context = CreateContext(activeTrip, preferences, logging);

        // Act
        var cut = context.Render<TripLocationPicker>(parameters => parameters
            .Add(component => component.Trip, Trip("Small lake near Clifden")));

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#trip-location-choices").Should().BeEmpty());
        OtherLocationValue(cut).Should().Be("Small lake near Clifden");
        await logging.Received(1).LogErrorAsync(
            "loading saved fishing locations",
            Arg.Any<InvalidOperationException>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPreserveATripPlaceThatIsNotSavedAsAPreference()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = TripServiceThatSaves();
        await using var context = CreateContext(activeTrip, PreferencesWith(Corrib(), Moy()));

        // Act
        var cut = context.Render<TripLocationPicker>(parameters => parameters
            .Add(component => component.Trip, Trip("Small lake near Clifden")));

        // Assert
        cut.WaitForAssertion(() =>
            OtherLocationValue(cut).Should().Be("Small lake near Clifden"));
        cut.Find("#trip-location-choice-lough-corrib").GetAttribute("aria-pressed").Should().Be("false");
        cut.Find("#trip-location-choice-river-moy").GetAttribute("aria-pressed").Should().Be("false");
        await activeTrip.DidNotReceive().UpdatePlaceAsync(
            Arg.Any<TripModel>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheSavedLocationMatchingTheTripAsSelected()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = TripServiceThatSaves();
        await using var context = CreateContext(activeTrip, PreferencesWith(Corrib(), Moy()));

        // Act
        var cut = context.Render<TripLocationPicker>(parameters => parameters
            .Add(component => component.Trip, Trip("lough corrib")));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-location-choice-lough-corrib").GetAttribute("aria-pressed").Should().Be("true"));
        cut.Find("#trip-location-choice-river-moy").GetAttribute("aria-pressed").Should().Be("false");
        OtherLocationValue(cut).Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task ItShouldClearTheTripLocation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = TripServiceThatSaves();
        await using var context = CreateContext(activeTrip, PreferencesWith(Corrib()));
        TripModel? changed = null;
        var cut = context.Render<TripLocationPicker>(parameters => parameters
            .Add(component => component.Trip, Trip("Lough Corrib"))
            .Add(component => component.OnPlaceChanged, trip => changed = trip));
        cut.WaitForAssertion(() => cut.Find("#trip-location-clear").Should().NotBeNull());

        // Act
        cut.Find("#trip-location-clear").Click();

        // Assert
        cut.WaitForAssertion(() => changed!.PlaceName.Should().BeNull());
        await activeTrip.Received(1).UpdatePlaceAsync(
            Arg.Is<TripModel>(trip => trip.Id == TripId),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReplaceTheTripPlaceWithAManualLocation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = TripServiceThatSaves();
        await using var context = CreateContext(activeTrip, PreferencesWith(Corrib()));
        TripModel? changed = null;
        var cut = context.Render<TripLocationPicker>(parameters => parameters
            .Add(component => component.Trip, Trip("Lough Corrib"))
            .Add(component => component.OnPlaceChanged, trip => changed = trip));
        cut.WaitForAssertion(() => cut.Find("#trip-location-other").Should().NotBeNull());

        // Act
        cut.Find("#trip-location-other").Change("Small lake near Clifden");

        // Assert
        cut.WaitForAssertion(() => changed!.PlaceName.Should().Be("Small lake near Clifden"));
        await activeTrip.Received(1).UpdatePlaceAsync(
            Arg.Is<TripModel>(trip => trip.Id == TripId),
            "Small lake near Clifden",
            Arg.Any<CancellationToken>());
        cut.Find("#trip-location-choice-lough-corrib").GetAttribute("aria-pressed").Should().Be("false");
    }

    [Fact]
    public async Task ItShouldCopyAnotherSavedLocationOntoTheTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = TripServiceThatSaves();
        await using var context = CreateContext(activeTrip, PreferencesWith(Corrib(), Moy()));
        TripModel? changed = null;
        var cut = context.Render<TripLocationPicker>(parameters => parameters
            .Add(component => component.Trip, Trip("Lough Corrib"))
            .Add(component => component.OnPlaceChanged, trip => changed = trip));
        cut.WaitForAssertion(() => cut.Find("#trip-location-choice-river-moy").Should().NotBeNull());

        // Act
        cut.Find("#trip-location-choice-river-moy").Click();

        // Assert
        cut.WaitForAssertion(() => changed!.PlaceName.Should().Be("River Moy"));
        cut.Find("#trip-location-choice-river-moy").GetAttribute("aria-pressed").Should().Be("true");
        cut.Find("#trip-location-choice-lough-corrib").GetAttribute("aria-pressed").Should().Be("false");
        await activeTrip.Received(1).UpdatePlaceAsync(
            Arg.Is<TripModel>(trip => trip.Id == TripId && trip.OwnerUserId == OwnerUserId),
            "River Moy",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotSaveWhenTheChosenLocationIsAlreadyTheTripPlace()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = TripServiceThatSaves();
        await using var context = CreateContext(activeTrip, PreferencesWith(Corrib()));
        var cut = context.Render<TripLocationPicker>(parameters => parameters
            .Add(component => component.Trip, Trip("Lough Corrib")));
        cut.WaitForAssertion(() => cut.Find("#trip-location-choice-lough-corrib").Should().NotBeNull());

        // Act
        cut.Find("#trip-location-choice-lough-corrib").Click();

        // Assert
        cut.Find("#trip-location-choice-lough-corrib").GetAttribute("aria-pressed").Should().Be("true");
        await activeTrip.DidNotReceive().UpdatePlaceAsync(
            Arg.Any<TripModel>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var activeTrip = TripServiceThatSaves();
        await using var context = CreateContext(activeTrip, PreferencesWith(Corrib()));

        // Act
        var cut = context.Render<TripLocationPicker>(parameters => parameters
            .Add(component => component.Trip, Trip("Lough Corrib")));

        // Assert
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Lieu de pêche"));
        cut.Markup.Should().Contain("Autre lieu");
        cut.Find("#trip-location-clear").TextContent.Should().Contain("Effacer le lieu");
    }
    private static string? OtherLocationValue(Bunit.IRenderedComponent<TripLocationPicker> cut)
    {
        return ((IHtmlInputElement)cut.Find("#trip-location-other")).Value;
    }
}
