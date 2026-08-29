using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchEdit;
using FishingLogBook.Web.Features.Trips.Clients;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchEditTests;

public class WhenTestingAnglerCorrection : BaseCatchEditTest
{
    private static readonly Guid TripId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    [Fact]
    public async Task ItShowsTheTripHeaderAndAnglerPickerForACollaborativeTrip()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(
                catchId,
                SyncStatus.Synchronised,
                SyncStatus.Synchronised,
                tripId: TripId,
                anglerUserId: OwnerUserId,
                recordedByUserId: OwnerUserId));
        var tripClient = GivenTripClient();
        var participantClient = GivenParticipantClient();
        await using var context = CreateContext(
            store,
            tripClient: tripClient,
            participantClient: participantClient,
            network: OnlineNetwork());

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-edit-trip-title").TextContent.Should().Contain("Costello & Fermoyle");
            cut.Find($"#catch-edit-angler-{OwnerUserId:D}").Should().NotBeNull();
            cut.Find($"#catch-edit-angler-{OtherUserId:D}").TextContent.Should().Contain("Patrick Connolly");
        });
    }

    [Fact]
    public async Task ItShouldCorrectTheAnglerWithoutChangingTheRecorder()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(
                catchId,
                SyncStatus.Synchronised,
                SyncStatus.Synchronised,
                tripId: TripId,
                anglerUserId: OwnerUserId,
                recordedByUserId: OwnerUserId));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var tripClient = GivenTripClient();
        var participantClient = GivenParticipantClient();
        var synchroniser = QuietSynchroniser();
        await using var context = CreateContext(
            store,
            synchroniser: synchroniser,
            tripClient: tripClient,
            participantClient: participantClient,
            network: OnlineNetwork());

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));
        cut.WaitForAssertion(() => cut.Find($"#catch-edit-angler-{OtherUserId:D}").Should().NotBeNull());
        await cut.Find($"#catch-edit-angler-{OtherUserId:D}").ClickAsync();

        // Assert
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(saved =>
                saved.Id == catchId
                && saved.UserId == OtherUserId
                && saved.AnglerUserId == OtherUserId
                && saved.RecordedByUserId == OwnerUserId
                && saved.MetadataSyncStatus == SyncStatus.WaitingToSynchronise),
            Arg.Any<CancellationToken>());
        await synchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotShowTheAnglerPickerForAStandaloneCatch()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(catchId, SyncStatus.Synchronised, SyncStatus.Synchronised));
        var tripClient = GivenTripClient();
        var participantClient = GivenParticipantClient();
        await using var context = CreateContext(
            store,
            tripClient: tripClient,
            participantClient: participantClient,
            network: OnlineNetwork());

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-edit-title").Should().NotBeNull());
        cut.FindAll("#catch-edit-trip-header").Should().BeEmpty();
        cut.FindAll("#catch-edit-angler-chips").Should().BeEmpty();
        await tripClient.DidNotReceive().GetDetailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private static ITripClient GivenTripClient()
    {
        var client = Substitute.For<ITripClient>();
        client.GetDetailAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(new TripDetailDto(new TripViewDto(
                TripId,
                OwnerUserId,
                "Active",
                StoredCaughtOn)
            {
                Title = "Costello & Fermoyle"
            }));
        return client;
    }

    private static ITripParticipantClient GivenParticipantClient()
    {
        var client = Substitute.For<ITripParticipantClient>();
        client.GetAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(new TripParticipantsDto(TripId, TripParticipantConstants.Owner)
            {
                Participants =
                [
                    new TripParticipantDto(OwnerUserId, TripParticipantConstants.Accepted, "Myles Costello", null, StoredCaughtOn)
                    {
                        IsOwner = true
                    },
                    new TripParticipantDto(OtherUserId, TripParticipantConstants.Accepted, "Patrick Connolly", null, StoredCaughtOn),
                    new TripParticipantDto(
                        Guid.Parse("33333333-3333-3333-3333-333333333333"),
                        TripParticipantConstants.Pending,
                        "Pending Angler",
                        null,
                        StoredCaughtOn)
                ]
            });
        return client;
    }
}
