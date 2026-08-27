using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ActiveTripPage = FishingLogBook.Web.Features.Trips.Pages.ActiveTrip.ActiveTrip;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.ActiveTripTests;

public class WhenTestingCatchCount : BaseActiveTripTest
{
    [Fact]
    public async Task ItShouldSayThereAreNoCatchesYet()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = await StoreWithActiveTripAsync();
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#active-trip-catch-count").TextContent.Should().Contain("No catches yet"));
    }

    [Fact]
    public async Task ItShouldCountOneCatchInTheSingular()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = await StoreWithActiveTripAsync();
        var catchStore = QuietCatchStore(CatchFor(TripId));
        await using var context = CreateContext(store, catchStore: catchStore);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#active-trip-catch-count").TextContent.Should().Contain("1 catch recorded"));
    }

    [Fact]
    public async Task ItShouldCountOnlyTheCatchesOnThisTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = await StoreWithActiveTripAsync();
        var catchStore = QuietCatchStore(
            CatchFor(TripId),
            CatchFor(TripId),
            CatchFor(Guid.NewGuid()),
            CatchFor(null));
        await using var context = CreateContext(store, catchStore: catchStore);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#active-trip-catch-count").TextContent.Should().Contain("2 catches recorded"));
    }

    [Fact]
    public async Task ItShouldReadCatchMetadataWithoutTouchingPhotographBytes()
    {
        // Arrange
        var store = await StoreWithActiveTripAsync();
        var catchStore = QuietCatchStore(CatchFor(TripId));
        await using var context = CreateContext(store, catchStore: catchStore);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-catch-count").Should().NotBeNull());
        await catchStore.Received(1).GetMetadataAsync(
            OwnerUserId,
            Arg.Any<CancellationToken>());
        await catchStore.DidNotReceive().GetAllAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await catchStore.DidNotReceive().GetAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillShowTheTripWhenTheCountCannotBeRead()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = await StoreWithActiveTripAsync();
        var catchStore = Substitute.For<ICatchStore>();
        catchStore.GetMetadataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("IndexedDB unavailable."));
        var logging = QuietLogging();
        await using var context = CreateContext(store, logging: logging, catchStore: catchStore);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-card").Should().NotBeNull());
        cut.Find("#active-trip-catch-count").TextContent.Should().Contain("No catches yet");
        cut.Find("#trip-catches-record").Should().NotBeNull();
        await logging.Received(1).LogErrorAsync(
            "reading the catches of a trip",
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchCatchCountCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var store = await StoreWithActiveTripAsync();
        var catchStore = QuietCatchStore(CatchFor(TripId), CatchFor(TripId));
        await using var context = CreateContext(store, catchStore: catchStore);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#active-trip-catch-count").TextContent
                .Should().Contain("2 prises enregistrées"));
    }
}
