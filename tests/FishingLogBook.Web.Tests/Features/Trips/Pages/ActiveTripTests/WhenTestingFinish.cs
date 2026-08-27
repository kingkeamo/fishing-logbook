using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;
using ActiveTripPage = FishingLogBook.Web.Features.Trips.Pages.ActiveTrip.ActiveTrip;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.ActiveTripTests;

public class WhenTestingFinish : BaseActiveTripTest
{
    [Fact]
    public async Task ItShouldFinishTheSameTripAndShowTheAcknowledgement()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(StoredActiveTrip());
        var activeTrip = QuietActiveTripService();
        await using var context = CreateContext(store, activeTrip);
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find("#active-trip-finish").Should().NotBeNull());

        // Act
        await cut.Find("#active-trip-finish").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#completed-trip-heading").Should().NotBeNull());
        cut.FindAll("#active-trip-finish").Should().BeEmpty();
        await activeTrip.Received(1).FinishAsync(
            Arg.Is<TripModel>(trip => trip.Id == TripId && trip.Status == TripConstants.Active),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotFinishATripThatIsAlreadyCompleted()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(StoredCompletedTrip());
        var activeTrip = QuietActiveTripService();
        await using var context = CreateContext(store, activeTrip);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#completed-trip-heading").Should().NotBeNull());
        await activeTrip.DidNotReceive().FinishAsync(
            Arg.Any<TripModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheFailureWhenFinishingFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(StoredActiveTrip());
        var activeTrip = QuietActiveTripService();
        activeTrip.FinishAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>())
            .Returns<Task<TripModel>>(_ => throw new TimeoutException("write timed out."));
        var logging = QuietLogging();
        await using var context = CreateContext(store, activeTrip, logging: logging);
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find("#active-trip-finish").Should().NotBeNull());

        // Act
        await cut.Find("#active-trip-finish").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-load-failed").Should().NotBeNull());
        await logging.Received(1).LogErrorAsync(
            "finishing a trip",
            Arg.Any<TimeoutException>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAttachAnOpportunisticLocationWithoutBlockingTheTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        var trip = StoredActiveTrip();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(trip);
        var activeTrip = QuietActiveTripService();
        activeTrip.TryAttachLocationAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>())
            .Returns(trip with { PlaceName = null });
        await using var context = CreateContext(store, activeTrip);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-card").Should().NotBeNull());
        await activeTrip.Received(1).TryAttachLocationAsync(
            Arg.Is<TripModel>(item => item.Id == TripId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotAttemptLocationForACompletedTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(StoredCompletedTrip());
        var activeTrip = QuietActiveTripService();
        await using var context = CreateContext(store, activeTrip);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#completed-trip-heading").Should().NotBeNull());
        await activeTrip.DidNotReceive().TryAttachLocationAsync(
            Arg.Any<TripModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillRenderTheTripWhenLocationCaptureFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(StoredActiveTrip());
        var activeTrip = QuietActiveTripService();
        activeTrip.TryAttachLocationAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>())
            .Returns<Task<TripModel?>>(_ => throw new InvalidOperationException("Geolocation is unavailable."));
        var logging = QuietLogging();
        await using var context = CreateContext(store, activeTrip, logging: logging);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-card").Should().NotBeNull());
        cut.FindAll("#trip-load-failed").Should().BeEmpty();
        await logging.Received(1).LogErrorAsync(
            "attaching a trip location",
            Arg.Any<InvalidOperationException>(),
            Arg.Any<CancellationToken>());
    }
}
