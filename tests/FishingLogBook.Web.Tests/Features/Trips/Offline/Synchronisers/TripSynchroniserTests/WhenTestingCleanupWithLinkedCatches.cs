using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Models;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Synchronisers.TripSynchroniserTests;

public class WhenTestingCleanupWithLinkedCatches : BaseTripSynchroniserTest
{
    private static readonly DateTimeOffset LongAgo = DateTimeOffset.UtcNow.AddDays(-30);

    [Fact]
    public async Task ItShouldKeepAnOldTripWhileALinkedCatchIsStillPending()
    {
        // Arrange
        GivenTripsAwaitingCatches(TripId);
        var store = await CreateStoreAsync(SyncedTrip(TripId));
        var sut = CreateSut(store);

        // Act
        await sut.CleanupSyncedCacheAsync(OwnerUserId, CancellationToken.None);

        // Assert
        var remaining = await store.GetAllAsync(OwnerUserId, CancellationToken.None);
        remaining.Should().ContainSingle(trip => trip.Id == TripId);
        store.RetainedTripIds.Should().Equal(TripId);
    }

    [Fact]
    public async Task ItShouldStillCleanUpAnUnrelatedOldTrip()
    {
        // Arrange
        GivenTripsAwaitingCatches(TripId);
        var store = await CreateStoreAsync(SyncedTrip(TripId), SyncedTrip(SecondTripId));
        var sut = CreateSut(store);

        // Act
        await sut.CleanupSyncedCacheAsync(OwnerUserId, CancellationToken.None);

        // Assert
        var remaining = await store.GetAllAsync(OwnerUserId, CancellationToken.None);
        remaining.Should().ContainSingle(trip => trip.Id == TripId);
    }

    [Fact]
    public async Task ItShouldCleanUpTheTripOnceItsLinkedCatchHasSynchronised()
    {
        // Arrange
        GivenTripsAwaitingCatches(TripId);
        var store = await CreateStoreAsync(SyncedTrip(TripId));
        var sut = CreateSut(store);
        await sut.CleanupSyncedCacheAsync(OwnerUserId, CancellationToken.None);

        // Act
        GivenTripsAwaitingCatches();
        await sut.CleanupSyncedCacheAsync(OwnerUserId, CancellationToken.None);

        // Assert
        var remaining = await store.GetAllAsync(OwnerUserId, CancellationToken.None);
        remaining.Should().BeEmpty();
        await MockTripDependency.Received(2).GetTripsAwaitingCatchesAsync(
            OwnerUserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAskOnlyForTheSignedInAnglersDependencies()
    {
        // Arrange
        GivenTripsAwaitingCatches();
        var store = await CreateStoreAsync(SyncedTrip(TripId));
        var sut = CreateSut(store);

        // Act
        await sut.CleanupSyncedCacheAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripDependency.Received(1).GetTripsAwaitingCatchesAsync(
            OwnerUserId,
            Arg.Any<CancellationToken>());
        await MockTripDependency.DidNotReceive().GetTripsAwaitingCatchesAsync(
            OtherUserId,
            Arg.Any<CancellationToken>());
    }

    private void GivenTripsAwaitingCatches(params Guid[] tripIds)
    {
        MockTripDependency.GetTripsAwaitingCatchesAsync(
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns<IReadOnlyCollection<Guid>>(tripIds);
    }

    private static TripModel SyncedTrip(Guid tripId)
    {
        return CreateTrip(
            tripId: tripId,
            status: TripConstants.Completed,
            endedOn: StartedOn.AddHours(2),
            syncStatus: SyncStatus.Synchronised,
            syncedAt: LongAgo);
    }
}
