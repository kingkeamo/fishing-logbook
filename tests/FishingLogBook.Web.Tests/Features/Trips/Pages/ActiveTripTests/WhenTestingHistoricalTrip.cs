using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Modals.AddTripNote;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Services;
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
        await tripClient.Received(1).GetDetailAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOfferAddNoteOnAnOwnedHistoricalTripWhileOnline()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns((TripModel?)null);
        var tripClient = HistoricalTripClient();
        await using var context = CreateContext(store, tripClient: tripClient);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-note-start").Should().NotBeNull());
        cut.Find("#trip-note-start").GetAttribute("aria-label").Should().Be("Add note");
        cut.FindAll("#active-trip-actions").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotOfferAddNoteOnAHistoricalTripWhileOffline()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns((TripModel?)null);
        var noteWriter = Substitute.For<ITripNoteWriteService>();
        await using var context = CreateContext(
            store,
            tripClient: HistoricalTripClient(),
            noteWriter: noteWriter,
            network: OnlineNetwork(false));

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-card").Should().NotBeNull());
        cut.FindAll("#trip-note-start").Should().BeEmpty();
        await noteWriter.DidNotReceive().AddAsync(
            Arg.Any<TripNoteDraftModel>(),
            Arg.Any<TripNoteStorageEnum>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowANewHistoricalNoteAtItsChosenPlaceOnTheTimeline()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var noteId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var catchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns((TripModel?)null);
        var recordedOn = StartedOn.AddMinutes(30);
        var tripClient = Substitute.For<ITripClient>();
        tripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(
                HistoricalDetail(catchId),
                HistoricalDetail(catchId) with
                {
                    Notes = [new TripNoteDto(noteId, TripId, "fish started rising", recordedOn)]
                });
        var modalService = ConfirmingModalService();
        modalService
            .ShowAsync<AddTripNoteModal, AddTripNoteModalModel, AddTripNoteModalResult>(
                Arg.Any<AddTripNoteModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(new AddTripNoteModalResult(
                new TripNoteModel(noteId, TripId, OwnerUserId, "fish started rising", recordedOn)));
        await using var context = CreateContext(
            store,
            tripClient: tripClient,
            modalService: modalService);
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find("#trip-note-start").Should().NotBeNull());

        // Act
        await cut.Find("#trip-note-start").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-timeline-note-{noteId:D}").TextContent
                .Should().Contain("fish started rising"));
        var rendered = cut.FindAll("[id^=trip-timeline-]")
            .Select(element => element.Id!)
            .ToArray();
        rendered.Should().ContainInOrder(
            $"trip-timeline-note-{noteId:D}",
            $"trip-timeline-catch-{catchId:D}");
        await tripClient.Received(2).GetDetailAsync(TripId, Arg.Any<CancellationToken>());
    }

    private static TripDetailDto HistoricalDetail(Guid catchId)
    {
        return new TripDetailDto(
            new TripViewDto(
                TripId,
                OwnerUserId,
                TripConstants.Completed,
                StartedOn,
                StartedOn.AddHours(5))
            {
                PlaceName = "Lough Corrib"
            })
        {
            Catches = [new TripCatchSummaryDto(catchId, StartedOn.AddHours(1)) { SpeciesName = "Pike" }]
        };
    }

    private static ITripClient HistoricalTripClient()
    {
        var tripClient = Substitute.For<ITripClient>();
        tripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(new TripDetailDto(
                new TripViewDto(
                    TripId,
                    OwnerUserId,
                    TripConstants.Completed,
                    StartedOn,
                    StartedOn.AddHours(5))
                {
                    PlaceName = "Lough Corrib"
                }));
        return tripClient;
    }
}
