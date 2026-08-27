using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ActiveTripPage = FishingLogBook.Web.Features.Trips.Pages.ActiveTrip.ActiveTrip;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.ActiveTripTests;

public class WhenTestingHistoricalTrip : BaseActiveTripTest
{
    [Fact]
    public async Task ItShouldSayTheTripIsNotFoundWhenNeitherTheDeviceNorTheServerHasIt()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns((TripModel?)null);
        var tripClient = Substitute.For<ITripClient>();
        tripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>()).Returns((TripDetailDto?)null);
        await using var context = CreateContext(store, tripClient: tripClient);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-not-found").Should().NotBeNull());
        await tripClient.Received(1).GetDetailAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheFailureWhenTheServerReadThrows()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns((TripModel?)null);
        var tripClient = Substitute.For<ITripClient>();
        tripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));
        var logging = QuietLogging();
        await using var context = CreateContext(store, logging: logging, tripClient: tripClient);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-load-failed").Should().NotBeNull());
        await logging.Received(1).LogErrorAsync(
            "loading a trip",
            Arg.Any<HttpRequestException>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotAskTheServerForATripThatIsStillOnTheDevice()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = await StoreWithActiveTripAsync();
        var tripClient = Substitute.For<ITripClient>();
        await using var context = CreateContext(store, tripClient: tripClient);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-card").Should().NotBeNull());
        await tripClient.DidNotReceive().GetDetailAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowAHistoricalTripFromTheServerWithItsPlaceAndTimeline()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns((TripModel?)null);
        var tripClient = Substitute.For<ITripClient>();
        tripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(new TripDetailDto(new TripViewDto(
                TripId,
                OwnerUserId,
                TripConstants.Completed,
                StartedOn,
                StartedOn.AddHours(5))
            {
                PlaceName = "Lough Corrib"
            })
            {
                Notes = [new TripNoteDto(Guid.NewGuid(), TripId, "The wind dropped.", StartedOn.AddMinutes(20))],
                Catches = [new TripCatchSummaryDto(catchId, StartedOn.AddHours(1)) { SpeciesName = "Pike" }]
            });
        await using var context = CreateContext(store, tripClient: tripClient);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-card").Should().NotBeNull());
        cut.Find("#active-trip-place").TextContent.Should().Contain("Lough Corrib");
        cut.Find($"#trip-timeline-catch-{catchId:D}").TextContent.Should().Contain("Pike");
        cut.Markup.Should().Contain("The wind dropped.");
        cut.FindAll("#trip-note-start").Should().BeEmpty();
        await tripClient.Received(1).GetDetailAsync(TripId, Arg.Any<CancellationToken>());
    }
}
