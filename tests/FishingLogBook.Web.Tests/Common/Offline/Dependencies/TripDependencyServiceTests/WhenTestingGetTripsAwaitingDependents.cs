using AwesomeAssertions;
using FishingLogBook.Web.Common;

namespace FishingLogBook.Web.Tests.Common.Offline.Dependencies.TripDependencyServiceTests;

public class WhenTestingGetTripsAwaitingDependents : BaseTripDependencyServiceTest
{
    [Fact]
    public async Task ItShouldReturnNothingWhenTheOwnerIsUnknown()
    {
        // Arrange
        await GivenCatchAsync(CatchId, TripId, SyncStatus.SavedLocally);

        // Act
        var awaiting = await Sut.GetTripsAwaitingDependentsAsync(Guid.Empty, CancellationToken.None);

        // Assert
        awaiting.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldIgnoreACatchWithNoTrip()
    {
        // Arrange
        await GivenCatchAsync(CatchId, tripId: null, SyncStatus.SavedLocally);

        // Act
        var awaiting = await Sut.GetTripsAwaitingDependentsAsync(OwnerUserId, CancellationToken.None);

        // Assert
        awaiting.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldIgnoreAFullySynchronisedCatch()
    {
        // Arrange
        await GivenCatchAsync(CatchId, TripId, SyncStatus.Synchronised);

        // Act
        var awaiting = await Sut.GetTripsAwaitingDependentsAsync(OwnerUserId, CancellationToken.None);

        // Assert
        awaiting.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldIgnoreAnotherAnglersPendingCatch()
    {
        // Arrange
        await GivenCatchAsync(CatchId, TripId, SyncStatus.SavedLocally, ownerUserId: OtherUserId);

        // Act
        var awaiting = await Sut.GetTripsAwaitingDependentsAsync(OwnerUserId, CancellationToken.None);

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
        var awaiting = await Sut.GetTripsAwaitingDependentsAsync(OwnerUserId, CancellationToken.None);

        // Assert
        awaiting.Should().Equal(TripId);
    }

    [Fact]
    public async Task ItShouldHoldTheTripWhenALinkedCatchFailedToSynchronise()
    {
        // Arrange
        await GivenCatchAsync(CatchId, TripId, SyncStatus.FailedToSynchronise);

        // Act
        var awaiting = await Sut.GetTripsAwaitingDependentsAsync(OwnerUserId, CancellationToken.None);

        // Assert
        awaiting.Should().Equal(TripId);
    }

    [Fact]
    public async Task ItShouldIgnoreASynchronisedTripPhotograph()
    {
        // Arrange
        await GivenTripPhotographAsync(Guid.NewGuid(), TripId, SyncStatus.Synchronised);

        // Act
        var awaiting = await Sut.GetTripsAwaitingDependentsAsync(OwnerUserId, CancellationToken.None);

        // Assert
        awaiting.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldIgnoreAnotherAnglersPendingTripPhotograph()
    {
        // Arrange
        await GivenTripPhotographAsync(
            Guid.NewGuid(),
            TripId,
            SyncStatus.SavedLocally,
            ownerUserId: OtherUserId);

        // Act
        var awaiting = await Sut.GetTripsAwaitingDependentsAsync(OwnerUserId, CancellationToken.None);

        // Assert
        awaiting.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldHoldTheTripWhileOneOfItsPhotographsIsStillPending()
    {
        // Arrange
        await GivenTripPhotographAsync(Guid.NewGuid(), TripId, SyncStatus.SavedLocally);

        // Act
        var awaiting = await Sut.GetTripsAwaitingDependentsAsync(OwnerUserId, CancellationToken.None);

        // Assert
        awaiting.Should().Equal(TripId);
        TripPhotographStore.BytesReadFor.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReportATripHeldByBothACatchAndAPhotographOnce()
    {
        // Arrange
        await GivenCatchAsync(CatchId, TripId, SyncStatus.SavedLocally);
        await GivenTripPhotographAsync(Guid.NewGuid(), TripId, SyncStatus.SavedLocally);
        await GivenTripPhotographAsync(Guid.NewGuid(), SecondTripId, SyncStatus.SavedLocally);

        // Act
        var awaiting = await Sut.GetTripsAwaitingDependentsAsync(OwnerUserId, CancellationToken.None);

        // Assert
        awaiting.Should().BeEquivalentTo([TripId, SecondTripId]);
        TripPhotographStore.BytesReadFor.Should().BeEmpty();
        CatchStore.PhotographBytesReadFor.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldIgnoreASynchronisedTripNote()
    {
        // Arrange
        await GivenTripNoteAsync(Guid.NewGuid(), TripId, SyncStatus.Synchronised);

        // Act
        var awaiting = await Sut.GetTripsAwaitingDependentsAsync(OwnerUserId, CancellationToken.None);

        // Assert
        awaiting.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldIgnoreAnotherAnglersPendingTripNote()
    {
        // Arrange
        await GivenTripNoteAsync(Guid.NewGuid(), TripId, SyncStatus.SavedLocally, ownerUserId: OtherUserId);

        // Act
        var awaiting = await Sut.GetTripsAwaitingDependentsAsync(OwnerUserId, CancellationToken.None);

        // Assert
        awaiting.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldHoldTheTripWhileOneOfItsNotesIsStillPending()
    {
        // Arrange
        await GivenTripNoteAsync(Guid.NewGuid(), TripId, SyncStatus.SavedLocally);

        // Act
        var awaiting = await Sut.GetTripsAwaitingDependentsAsync(OwnerUserId, CancellationToken.None);

        // Assert
        awaiting.Should().Equal(TripId);
        TripPhotographStore.BytesReadFor.Should().BeEmpty();
        CatchStore.PhotographBytesReadFor.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReportATripHeldByACatchAPhotographAndANoteOnce()
    {
        // Arrange
        await GivenCatchAsync(CatchId, TripId, SyncStatus.SavedLocally);
        await GivenTripPhotographAsync(Guid.NewGuid(), TripId, SyncStatus.SavedLocally);
        await GivenTripNoteAsync(Guid.NewGuid(), TripId, SyncStatus.SavedLocally);
        await GivenTripNoteAsync(Guid.NewGuid(), SecondTripId, SyncStatus.SavedLocally);

        // Act
        var awaiting = await Sut.GetTripsAwaitingDependentsAsync(OwnerUserId, CancellationToken.None);

        // Assert
        awaiting.Should().BeEquivalentTo([TripId, SecondTripId]);
        TripPhotographStore.BytesReadFor.Should().BeEmpty();
        CatchStore.PhotographBytesReadFor.Should().BeEmpty();
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
        var awaiting = await Sut.GetTripsAwaitingDependentsAsync(OwnerUserId, CancellationToken.None);

        // Assert
        awaiting.Should().BeEquivalentTo([TripId, SecondTripId]);
        CatchStore.PhotographBytesReadFor.Should().BeEmpty();
        CatchStore.GetAllCalls.Should().Be(0);
        CatchStore.GetMetadataCalls.Should().Be(1);
    }
}
