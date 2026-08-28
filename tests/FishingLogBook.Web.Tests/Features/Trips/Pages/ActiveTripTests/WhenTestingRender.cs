using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ActiveTripPage = FishingLogBook.Web.Features.Trips.Pages.ActiveTrip.ActiveTrip;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.ActiveTripTests;

public class WhenTestingRender : BaseActiveTripTest
{
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
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-load-failed").TextContent.Should().Contain("could not be loaded"));
        cut.Find("#trip-load-retry").TextContent.Should().Contain("Try again");
    }

    [Fact]
    public async Task ItShouldReloadWhenRetryIsPressed()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        var reads = 0;
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                reads += 1;
                return reads == 1
                    ? throw new TimeoutException("single-read timed out.")
                    : Task.FromResult<TripModel?>(StoredActiveTrip());
            });
        await using var context = CreateContext(store);
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find("#trip-load-retry").Should().NotBeNull());

        // Act
        await cut.Find("#trip-load-retry").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-card").Should().NotBeNull());
        await store.Received(2).GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheUnavailableMessageWhenTheTripIsNotStored()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns((TripModel?)null);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-not-found").TextContent.Should().Contain("no longer available"));
        cut.FindAll("#active-trip-card").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRenderTheActiveTripFromThePersistedStore()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(StoredActiveTrip());
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-card").Should().NotBeNull());
        cut.Find("#active-trip-status").TextContent.Should().Contain("Active trip");
        cut.Find("#active-trip-started").TextContent.Should().NotBeEmpty();
        cut.Find("#active-trip-finish").TextContent.Should().Contain("Finish trip");
        await store.Received(1).GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowAGeneratedDateWhenTheTripHasNoTitle()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(StoredActiveTrip());
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#active-trip-heading").TextContent.Should().Contain("2026"));
        await store.DidNotReceive().SaveAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheTitleAndPlaceWhenTheyAreSet()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(StoredActiveTrip(title: "Day with Dad", placeName: "Lough Corrib"));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#active-trip-heading").TextContent.Should().Contain("Day with Dad"));
        cut.Find("#active-trip-place").TextContent.Should().Contain("Lough Corrib");
        cut.Find("#active-trip-facts").ClassName.Should().Contain("mud-grid");
        cut.Find(".active-trip-fact-place").ClassName.Should().Contain("mud-grid-item-xs-6");
        cut.Find(".active-trip-fact-started").ClassName.Should().Contain("mud-grid-item-xs-3");
        cut.Find(".active-trip-fact-elapsed").ClassName.Should().Contain("mud-grid-item-xs-3");
        cut.Find("#active-trip-stats").ClassName.Should().Contain("mud-grid");
        cut.Find(".active-trip-stat-catches").ClassName.Should().Contain("mud-grid-item-xs-6");
        cut.Find(".active-trip-stat-photos").ClassName.Should().Contain("mud-grid-item-xs-3");
        cut.Find(".active-trip-stat-notes").ClassName.Should().Contain("mud-grid-item-xs-3");
    }

    [Fact]
    public async Task ItShouldRenderTheCompletedSummaryForAFinishedTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(StoredCompletedTrip());
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#active-trip-status").TextContent.Should().Contain("Finished"));
        cut.Find("#active-trip-elapsed").TextContent.Should().Contain("6h 43m");
        cut.Find("#active-trip-logbook").TextContent.Should().Contain("Back to logbook");
        cut.FindAll("#active-trip-finish").Should().BeEmpty();
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
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#active-trip-finish").TextContent.Should().Contain("Terminer la sortie"));
        cut.Find("#active-trip-status").TextContent.Should().Contain("Sortie en cours");
    }
}
