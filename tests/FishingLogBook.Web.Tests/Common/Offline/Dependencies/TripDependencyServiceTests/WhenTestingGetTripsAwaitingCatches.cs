using AwesomeAssertions;
using FishingLogBook.Web.Common;

namespace FishingLogBook.Web.Tests.Common.Offline.Dependencies.TripDependencyServiceTests;

public class WhenTestingGetTripsAwaitingCatches : BaseTripDependencyServiceTest
{
    [Fact]
    public async Task ItShouldReturnNothingWhenTheOwnerIsUnknown()
    {
        // Arrange
        await GivenCatchAsync(CatchId, TripId, SyncStatus.SavedLocally);

        // Act
        var awaiting = await Sut.GetTripsAwaitingCatchesAsync(Guid.Empty, CancellationToken.None);

        // Assert
        awaiting.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldIgnoreACatchWithNoTrip()
    {
        // Arrange
        await GivenCatchAsync(CatchId, tripId: null, SyncStatus.SavedLocally);

        // Act
        var awaiting = await Sut.GetTripsAwaitingCatchesAsync(OwnerUserId, CancellationToken.None);

        // Assert
        awaiting.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldIgnoreAFullySynchronisedCatch()
    {
        // Arrange
        await GivenCatchAsync(CatchId, TripId, SyncStatus.Synchronised);

        // Act
        var awaiting = await Sut.GetTripsAwaitingCatchesAsync(OwnerUserId, CancellationToken.None);

        // Assert
        awaiting.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldIgnoreAnotherAnglersPendingCatch()
    {
        // Arrange
        await GivenCatchAsync(CatchId, TripId, SyncStatus.SavedLocally, ownerUserId: OtherUserId);

        // Act
        var awaiting = await Sut.GetTripsAwaitingCatchesAsync(OwnerUserId, CancellationToken.None);

        // Assert
        awaiting.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldHoldTheTripWhenOnlyTheMetadataIsStillPending()
    {
        // Arrange
        await GivenCatchAsync(
            CatchId,
            TripId,
            SyncStatus.Synchronised,
            metadataSyncStatus: SyncStatus.SavedLocally);

        // Act
        var awaiting = await Sut.GetTripsAwaitingCatchesAsync(OwnerUserId, CancellationToken.None);

        // Assert
        awaiting.Should().Equal(TripId);
    }

    [Fact]
    public async Task ItShouldHoldTheTripWhenALinkedCatchFailedToSynchronise()
    {
        // Arrange
        await GivenCatchAsync(CatchId, TripId, SyncStatus.FailedToSynchronise);

        // Act
        var awaiting = await Sut.GetTripsAwaitingCatchesAsync(OwnerUserId, CancellationToken.None);

        // Assert
        awaiting.Should().Equal(TripId);
    }

    [Fact]
    public async Task ItShouldReportEachTripOnceWithoutReadingPhotographBytes()
    {
        // Arrange
        await GivenCatchAsync(CatchId, TripId, SyncStatus.SavedLocally);
        await GivenCatchAsync(Guid.NewGuid(), TripId, SyncStatus.SavedLocally);
        await GivenCatchAsync(Guid.NewGuid(), SecondTripId, SyncStatus.SavedLocally);
        await GivenCatchAsync(Guid.NewGuid(), Guid.NewGuid(), SyncStatus.Synchronised);

        // Act
        var awaiting = await Sut.GetTripsAwaitingCatchesAsync(OwnerUserId, CancellationToken.None);

        // Assert
        awaiting.Should().BeEquivalentTo([TripId, SecondTripId]);
        CatchStore.PhotographBytesReadFor.Should().BeEmpty();
        CatchStore.GetAllCalls.Should().Be(0);
        CatchStore.GetMetadataCalls.Should().Be(1);
    }
}
