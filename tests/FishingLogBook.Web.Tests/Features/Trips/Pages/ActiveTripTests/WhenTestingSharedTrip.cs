using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using NSubstitute;
using ActiveTripPage = FishingLogBook.Web.Features.Trips.Pages.ActiveTrip.ActiveTrip;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.ActiveTripTests;

public class WhenTestingSharedTrip : BaseActiveTripTest
{
    private static readonly Guid SharedOwnerUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task ItShouldNotCacheATripTheAnglerIsOnlyViewing()
    {
        // Arrange
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns((TripModel?)null);
        var tripClient = ClientReturning(TripParticipantConstants.None);
        await using var context = CreateContext(store, tripClient: tripClient);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-card").Should().NotBeNull());
        await store.DidNotReceive().HydrateAsync(
            Arg.Any<TripModel>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldHideTheOwnerOnlyActionsFromAParticipant()
    {
        // Arrange
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns((TripModel?)null);
        var tripClient = ClientReturning(TripParticipantConstants.Participant);
        await using var context = CreateContext(store, tripClient: tripClient);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-card").Should().NotBeNull());
        cut.FindAll("#active-trip-finish").Should().BeEmpty();
        cut.FindAll("#active-trip-edit").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldHideTheOwnerOnlyActionsFromALocallyCachedSharedTrip()
    {
        // Arrange
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(CachedSharedTrip());
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-card").Should().NotBeNull());
        cut.FindAll("#active-trip-finish").Should().BeEmpty();
        cut.FindAll("#active-trip-edit").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldOfferTheOwnerTheirOwnTripActions()
    {
        // Arrange
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(StoredActiveTrip());
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-card").Should().NotBeNull());
        cut.Find("#active-trip-finish").Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldCacheAnActiveSharedTripForTheParticipantOffline()
    {
        // Arrange
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns((TripModel?)null);
        var tripClient = ClientReturning(TripParticipantConstants.Participant);
        await using var context = CreateContext(store, tripClient: tripClient);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-card").Should().NotBeNull());
        await store.Received(1).HydrateAsync(
            Arg.Is<TripModel>(trip =>
                trip.Id == TripId
                && trip.OwnerUserId == SharedOwnerUserId
                && trip.Origin == TripOriginEnum.Server
                && trip.ParticipantUserIds.Contains(OwnerUserId)),
            OwnerUserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLetAParticipantRecordACatchAndAddAPhotographOnASharedTrip()
    {
        // Arrange
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(CachedSharedTrip());
        var tripClient = ClientReturning(TripParticipantConstants.Participant);
        await using var context = CreateContext(store, tripClient: tripClient);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-record-catch").Should().NotBeNull());
        cut.Find("#active-trip-add-photo").Should().NotBeNull();
        cut.Find("#active-trip-add-catch").Should().NotBeNull();
        cut.Find("#trip-note-start").Should().NotBeNull();
        cut.FindAll("#active-trip-finish").Should().BeEmpty();
        cut.FindAll("#active-trip-update").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowTheOwnersNotesToAParticipantAfterTheSharedTripRefreshes()
    {
        // Arrange
        var noteId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(CachedSharedTrip());
        var tripClient = ClientReturning(
            TripParticipantConstants.Participant,
            notes: [new TripNoteDto(noteId, TripId, "the owner note", StartedOn.AddMinutes(20))
            {
                CreatedByUserId = SharedOwnerUserId
            }]);
        await using var context = CreateContext(store, tripClient: tripClient);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("the owner note"));
        cut.Find("#active-trip-note-count").TextContent.Should().Contain("1");
        await tripClient.Received(1).GetDetailAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepAParticipantsUnsyncedNoteVisibleAlongsideTheServerDiary()
    {
        // Arrange
        var serverNoteId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var localNoteId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(CachedSharedTrip(new TripNoteModel(
                localNoteId,
                TripId,
                OwnerUserId,
                "my note not yet synced",
                StartedOn.AddMinutes(40))));
        var tripClient = ClientReturning(
            TripParticipantConstants.Participant,
            notes: [new TripNoteDto(serverNoteId, TripId, "the owner note", StartedOn.AddMinutes(20))
            {
                CreatedByUserId = SharedOwnerUserId
            }]);
        await using var context = CreateContext(store, tripClient: tripClient);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("my note not yet synced"));
        cut.Markup.Should().Contain("the owner note");
        cut.Find("#active-trip-note-count").TextContent.Should().Contain("2");
    }

    [Fact]
    public async Task ItShouldOfferTheParticipantTheSharedParticipantsSurface()
    {
        // Arrange
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns((TripModel?)null);
        var tripClient = ClientReturning(TripParticipantConstants.Participant);
        await using var context = CreateContext(store, tripClient: tripClient);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-participants").Should().NotBeNull());
        await tripClient.Received(1).GetDetailAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRevokeAndStopShowingAServerOriginTripAfterTheOwnerRemovesThisParticipant()
    {
        // Arrange - the authoritative server no longer returns this Trip for the current
        // viewer (they were removed as a participant), but the locally cached copy still
        // lists them - it must not keep presenting as writable on that stale evidence.
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(CachedSharedTrip(), (TripModel?)null);
        var tripClient = Substitute.For<ITripClient>();
        tripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>())
            .Returns((TripDetailDto?)null);
        await using var context = CreateContext(store, tripClient: tripClient);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters =>
            parameters.Add(page => page.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-not-found").Should().NotBeNull());
        await store.Received(1).RevokeParticipantAccessAsync(
            OwnerUserId,
            TripId,
            Arg.Any<CancellationToken>());
    }

    private static ITripClient ClientReturning(
        string role,
        IReadOnlyList<TripNoteDto>? notes = null)
    {
        var client = Substitute.For<ITripClient>();
        client.GetDetailAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(new TripDetailDto(
                new TripViewDto(
                    TripId,
                    SharedOwnerUserId,
                    TripConstants.Active,
                    StartedOn))
            {
                Role = role,
                Notes = notes ?? [],
                Contributors =
                [
                    new TripContributorDto(SharedOwnerUserId, "Mark", null) { IsOwner = true }
                ]
            });
        return client;
    }

    private static TripModel CachedSharedTrip(params TripNoteModel[] notes)
    {
        return new TripModel(
            TripId,
            SharedOwnerUserId,
            TripConstants.Active,
            StartedOn,
            SyncStatus: SyncStatus.Synchronised,
            SyncedAt: StartedOn,
            Notes: notes.Length == 0 ? null : notes,
            ParticipantUserIds: [OwnerUserId],
            Origin: TripOriginEnum.Server);
    }
}
