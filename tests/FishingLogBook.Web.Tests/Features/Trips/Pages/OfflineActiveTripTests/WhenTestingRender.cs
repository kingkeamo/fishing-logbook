using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.Features.Trips.Pages.ActiveTripTests;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OfflineActiveTripPage = FishingLogBook.Web.Features.Trips.Pages.OfflineActiveTrip.OfflineActiveTrip;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.OfflineActiveTripTests;

public class WhenTestingRender : BaseActiveTripTest
{
    [Fact]
    public async Task ItShouldFailClosedWhenTheOfflineOwnerIsMissing()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        await using var context = CreateContext(store, offlineOwner: LockedOfflineOwner());

        // Act
        var cut = context.Render<OfflineActiveTripPage>(
            parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#offline-trip-load-failed").Should().NotBeNull());
        await store.DidNotReceive().GetAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReadOnlyFromTheLocalStoreForTheUnlockedOwner()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(StoredActiveTrip());
        var onlineOwner = SignedInOwner();
        await using var context = CreateContext(store, owner: onlineOwner);

        // Act
        var cut = context.Render<OfflineActiveTripPage>(
            parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-card").Should().NotBeNull());
        await store.Received(1).GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>());
        await onlineOwner.DidNotReceive().GetUserIdAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotReturnATripBelongingToAnotherOwner()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns((TripModel?)null);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<OfflineActiveTripPage>(
            parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#offline-trip-not-found").Should().NotBeNull());
        cut.FindAll("#active-trip-card").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowTheFailureWithRetryWhenTheLocalReadThrows()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("single-read timed out."));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<OfflineActiveTripPage>(
            parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#offline-trip-load-failed").Should().NotBeNull());
        cut.Find("#offline-trip-load-retry").TextContent.Should().Contain("Try again");
    }

    [Fact]
    public async Task ItShouldFinishATripOfflineWithoutAnyOnlineOwnerLookup()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(StoredActiveTrip());
        var activeTrip = QuietActiveTripService();
        var onlineOwner = SignedInOwner();
        await using var context = CreateContext(store, activeTrip, owner: onlineOwner);
        var cut = context.Render<OfflineActiveTripPage>(
            parameters => parameters.Add(p => p.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find("#active-trip-finish").Should().NotBeNull());

        // Act
        await cut.Find("#active-trip-finish").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#completed-trip-heading").Should().NotBeNull());
        cut.Find("#completed-trip-logbook").GetAttribute("href").Should().Be("/offline/catches");
        await activeTrip.Received(1).FinishAsync(
            Arg.Is<TripModel>(trip => trip.Id == TripId),
            Arg.Any<CancellationToken>());
        await onlineOwner.DidNotReceive().GetUserIdAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotAttemptLocationCaptureOnTheOfflineRoute()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(StoredActiveTrip());
        var activeTrip = QuietActiveTripService();
        await using var context = CreateContext(store, activeTrip);

        // Act
        var cut = context.Render<OfflineActiveTripPage>(
            parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-card").Should().NotBeNull());
        await activeTrip.DidNotReceive().TryAttachLocationAsync(
            Arg.Any<TripModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(StoredActiveTrip());
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<OfflineActiveTripPage>(
            parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#active-trip-finish").TextContent.Should().Contain("Terminer la sortie"));
    }
}
