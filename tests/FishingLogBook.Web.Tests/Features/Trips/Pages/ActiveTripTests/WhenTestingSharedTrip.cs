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

    private static ITripClient ClientReturning(string role)
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
                Contributors =
                [
                    new TripContributorDto(SharedOwnerUserId, "Mark", null) { IsOwner = true }
                ]
            });
        return client;
    }

    private static TripModel CachedSharedTrip()
    {
        return new TripModel(
            TripId,
            SharedOwnerUserId,
            TripConstants.Active,
            StartedOn,
            SyncStatus: SyncStatus.Synchronised,
            SyncedAt: StartedOn,
            ParticipantUserIds: [OwnerUserId],
            Origin: TripOriginEnum.Server);
    }
}
