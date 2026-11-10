using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.Features.Trips.Pages.ActiveTripTests;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OfflineActiveTripPage = FishingLogBook.Web.Features.Trips.Pages.OfflineActiveTrip.OfflineActiveTrip;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.OfflineActiveTripTests;

public class WhenTestingResilience : BaseActiveTripTest
{
    [Fact]
    public async Task ItShouldStillShowTheTripAndLogWhenTheLocalCatchesCannotBeRead()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(StoredActiveTrip());
        var catchStore = Substitute.For<ICatchStore>();
        catchStore.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("catches timed out"));
        var logging = QuietLogging();
        await using var context = CreateContext(store, logging: logging, catchStore: catchStore);

        // Act
        var cut = context.Render<OfflineActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-card").Should().NotBeNull());
        cut.FindAll("#offline-trip-load-failed").Should().BeEmpty();
        await logging.Received(1).LogErrorAsync(
            "reading the catches of a trip offline",
            Arg.Is<Exception>(exception => exception.Message == "catches timed out"),
            CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldNotOfferToFinishACompletedTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(StoredCompletedTrip());
        var activeTrip = QuietActiveTripService();
        await using var context = CreateContext(store, activeTrip);

        // Act
        var cut = context.Render<OfflineActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-status").TextContent.Should().Contain("Finished"));
        cut.FindAll("#active-trip-finish").Should().BeEmpty();
        await activeTrip.DidNotReceive().FinishAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotOfferToFinishATripOwnedByAnotherAngler()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var otherOwner = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(StoredActiveTrip() with { OwnerUserId = otherOwner });
        var activeTrip = QuietActiveTripService();
        await using var context = CreateContext(store, activeTrip);

        // Act
        var cut = context.Render<OfflineActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-card").Should().NotBeNull());
        cut.FindAll("#active-trip-finish").Should().BeEmpty();
        await activeTrip.DidNotReceive().FinishAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheFailureAndLogWhenFinishingTheTripFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(StoredActiveTrip());
        var activeTrip = QuietActiveTripService();
        activeTrip.FinishAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("cannot finish"));
        var logging = QuietLogging();
        await using var context = CreateContext(store, activeTrip, logging: logging);
        var cut = context.Render<OfflineActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find("#active-trip-finish").Should().NotBeNull());

        // Act
        await cut.Find("#active-trip-finish").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#offline-trip-load-failed").Should().NotBeNull());
        await logging.Received(1).LogErrorAsync(
            "finishing a trip offline",
            Arg.Is<Exception>(exception => exception.Message == "cannot finish"),
            CancellationToken.None);
        await activeTrip.Received(1).FinishAsync(
            Arg.Is<TripModel>(trip => trip.Id == TripId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReloadTheTripWhenTheAnglerRetriesAfterAFailedLoad()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException<TripModel?>(new TimeoutException("first")),
                _ => Task.FromResult<TripModel?>(StoredActiveTrip()));
        await using var context = CreateContext(store);
        var cut = context.Render<OfflineActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find("#offline-trip-load-retry").Should().NotBeNull());

        // Act
        await cut.Find("#offline-trip-load-retry").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-card").Should().NotBeNull());
        cut.FindAll("#offline-trip-load-failed").Should().BeEmpty();
        await store.Received(2).GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>());
    }
}
