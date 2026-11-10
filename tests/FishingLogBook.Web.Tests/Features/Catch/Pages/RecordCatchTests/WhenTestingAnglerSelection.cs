using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.RecordCatch;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.RecordCatchTests;

public class WhenTestingAnglerSelection : BaseRecordCatchTest
{
    private static readonly Guid TripId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid AcceptedGuestId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid NamelessGuestId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid PendingGuestId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T09:00:00Z");

    private static TripModel Trip()
    {
        return new TripModel(TripId, OwnerUserId, TripConstants.Active, StartedOn);
    }

    private static IActiveTripService ActiveTrip()
    {
        var activeTrip = Substitute.For<IActiveTripService>();
        activeTrip.GetActiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Trip());
        return activeTrip;
    }

    private static ITripStore TripStore()
    {
        var tripStore = Substitute.For<ITripStore>();
        tripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(Trip());
        return tripStore;
    }

    private static ITripParticipantClient Participants(params TripParticipantDto[] participants)
    {
        var client = Substitute.For<ITripParticipantClient>();
        client.GetAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(new TripParticipantsDto(TripId, TripParticipantConstants.Owner) { Participants = participants });
        return client;
    }

    private static TripParticipantDto Participant(
        Guid userId,
        string status,
        string? displayName,
        bool isOwner = false)
    {
        return new TripParticipantDto(userId, status, displayName, null, StartedOn) { IsOwner = isOwner };
    }

    [Fact]
    public async Task ItShouldOfferEveryAcceptedAnglerOnTheTripAndSaveTheCatchForTheChosenOne()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var client = Participants(
            Participant(OwnerUserId, TripParticipantConstants.Accepted, "Eamonn", isOwner: true),
            Participant(AcceptedGuestId, TripParticipantConstants.Accepted, "Mary"),
            Participant(NamelessGuestId, TripParticipantConstants.Accepted, " "),
            Participant(PendingGuestId, TripParticipantConstants.Pending, "Pending Pat"));
        await using var context = CreateContext(
            store,
            tripStore: TripStore(),
            activeTrip: ActiveTrip(),
            participantClient: client);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));
        cut.WaitForAssertion(() => cut.Find("#record-catch-angler-chips").Should().NotBeNull());

        // Assert
        var chips = cut.Find("#record-catch-angler-chips").TextContent;
        chips.Should().Contain("Me").And.Contain("Mary").And.Contain("Another angler").And.NotContain("Pending Pat");
        cut.FindAll($"#record-catch-angler-{PendingGuestId:D}").Should().BeEmpty();

        // Act
        await cut.Find($"#record-catch-angler-{AcceptedGuestId:D}").ClickAsync();
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(saved => saved.TripId == TripId
                && saved.CaughtByUserId == AcceptedGuestId
                && saved.RecordedByUserId == OwnerUserId),
            Arg.Any<CancellationToken>());
        await client.Received(1).GetAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotOfferAnAnglerChoiceWhenTheOwnerIsTheOnlyAnglerOnTheTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var client = Participants(Participant(OwnerUserId, TripParticipantConstants.Accepted, "Eamonn", isOwner: true));
        await using var context = CreateContext(
            Substitute.For<ICatchStore>(),
            tripStore: TripStore(),
            activeTrip: ActiveTrip(),
            participantClient: client);

        // Act
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => cut.Find("#catch-trip-association").Should().NotBeNull());

        // Assert
        cut.FindAll("#record-catch-angler-chips").Should().BeEmpty();
        await client.Received(1).GetAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillSaveForTheOwnerWhenTheTripAnglersCannotBeLoaded()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var client = Substitute.For<ITripParticipantClient>();
        client.GetAsync(TripId, Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("offline"));
        var logging = QuietLogging();
        await using var context = CreateContext(
            store,
            logging: logging,
            tripStore: TripStore(),
            activeTrip: ActiveTrip(),
            participantClient: client);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.FindAll("#record-catch-angler-chips").Should().BeEmpty();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(saved => saved.TripId == TripId && saved.CaughtByUserId == OwnerUserId),
            Arg.Any<CancellationToken>());
        await logging.Received(1).LogErrorAsync(
            "loading trip anglers",
            Arg.Is<Exception>(exception => exception.Message == "offline"),
            CancellationToken.None);
    }
}
