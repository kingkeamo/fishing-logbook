using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Components.TripEditor;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripEditorTests;

public class WhenTestingSave : BaseTripEditorTest
{
    [Fact]
    public async Task ItShouldSaveTheEditedTitleAndPlaceAndReturnToTheDiary()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var trip = ActiveTrip(title: "Morning session", placeName: "Lough Corrib");
        var activeTrip = Substitute.For<IActiveTripService>();
        activeTrip
            .UpdateDetailsAsync(Arg.Any<TripModel>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => trip with { Title = call.ArgAt<string?>(1), PlaceName = call.ArgAt<string?>(2) });
        await using var context = CreateContext(activeTrip: activeTrip);
        var closed = 0;
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, trip)
            .Add(component => component.OnClosed, () => closed++));

        // Act
        cut.Find("#trip-editor-title").Input("Afternoon session");
        cut.Find("#trip-location-other").Change("River Moy");
        await cut.Find("#trip-editor-save").ClickAsync();

        // Assert
        closed.Should().Be(1);
        await activeTrip.Received(1).UpdateDetailsAsync(
            Arg.Is<TripModel>(saved => saved.Id == TripId),
            "Afternoon session",
            "River Moy",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotSaveWhenCancelled()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var trip = ActiveTrip(title: "Morning session", placeName: "Lough Corrib");
        var activeTrip = Substitute.For<IActiveTripService>();
        await using var context = CreateContext(activeTrip: activeTrip);
        var closed = 0;
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, trip)
            .Add(component => component.OnClosed, () => closed++));

        // Act
        cut.Find("#trip-editor-title").Input("Should not be saved");
        await cut.Find("#trip-editor-cancel").ClickAsync();

        // Assert
        closed.Should().Be(1);
        await activeTrip.DidNotReceive().UpdateDetailsAsync(
            Arg.Any<TripModel>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowAFailureAndStayOpenWhenTheSaveThrows()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var trip = ActiveTrip();
        var activeTrip = Substitute.For<IActiveTripService>();
        activeTrip
            .UpdateDetailsAsync(Arg.Any<TripModel>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("write failed"));
        var logging = Substitute.For<ILoggingService>();
        await using var context = CreateContext(activeTrip: activeTrip, logging: logging);
        var closed = 0;
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, trip)
            .Add(component => component.OnClosed, () => closed++));

        // Act
        await cut.Find("#trip-editor-save").ClickAsync();

        // Assert
        cut.Find("#trip-editor-save-failed").TextContent.Should().Contain("could not be saved");
        closed.Should().Be(0);
        await logging.Received(1).LogErrorAsync(
            "saving trip details",
            Arg.Any<InvalidOperationException>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowAFailureWhenTheTripIsNoLongerStored()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var trip = ActiveTrip();
        var activeTrip = Substitute.For<IActiveTripService>();
        activeTrip
            .UpdateDetailsAsync(Arg.Any<TripModel>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((TripModel?)null);
        await using var context = CreateContext(activeTrip: activeTrip);
        var closed = 0;
        var cut = context.Render<TripEditor>(parameters => parameters
            .Add(component => component.Trip, trip)
            .Add(component => component.OnClosed, () => closed++));

        // Act
        await cut.Find("#trip-editor-save").ClickAsync();

        // Assert
        cut.Find("#trip-editor-save-failed").Should().NotBeNull();
        closed.Should().Be(0);
    }
}
