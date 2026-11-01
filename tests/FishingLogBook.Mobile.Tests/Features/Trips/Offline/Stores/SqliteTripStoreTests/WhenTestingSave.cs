using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Offline.Stores.SqliteTripStoreTests;

public sealed class WhenTestingSave : BaseSqliteTripStoreTest
{
    [Fact]
    public async Task ItShouldRefuseATripForAnotherAccount()
    {
        // Arrange
        var trip = NewTrip(userId: OtherUserId);
        // Act
        var act = () => TripStore.SaveAsync(OwnerLease, trip, 0, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await TripStore.ListAsync(OtherUserId, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRefuseATripWithoutAnAccount()
    {
        // Arrange
        var trip = NewTrip(userId: Guid.Empty);
        // Act
        var act = () => TripStore.SaveAsync(OwnerLease, trip, 0, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ItShouldStopWhenTheAccountSessionEndedBeforeTheCommit()
    {
        // Arrange
        RevokeSession();
        // Act
        var act = () => TripStore.SaveAsync(OwnerLease, NewTrip(), 0, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        (await TripStore.ListAsync(OwnerUserId, CancellationToken.None)).Should().BeEmpty();
        (await PendingAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRefuseAnEditMadeFromAStaleRevisionAndQueueNothingExtra()
    {
        // Arrange
        var trip = NewTrip();
        await TripStore.SaveAsync(OwnerLease, trip, 0, CancellationToken.None);
        await TripStore.SaveAsync(OwnerLease, trip, 1, CancellationToken.None);
        // Act
        var act = () => TripStore.SaveAsync(OwnerLease, trip, 1, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await TripStore.GetAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None))!.Revision.Should().Be(2);
    }

    [Fact]
    public async Task ItShouldSaveTheTripAndQueueOneMetadataUpsertThatIsNotDuplicatedByASecondSave()
    {
        // Arrange
        var trip = NewTrip();
        // Act
        await TripStore.SaveAsync(OwnerLease, trip, 0, CancellationToken.None);
        await TripStore.SaveAsync(OwnerLease, trip with { Detail = trip.Detail with { Trip = trip.Detail.Trip with { Title = "Edited" } } }, 1, CancellationToken.None);
        // Assert
        var stored = await TripStore.GetAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None);
        stored!.Detail.Trip.Title.Should().Be("Edited");
        stored.Revision.Should().Be(2);
        stored.HasPendingMetadata.Should().BeTrue();
        (await PendingAsync()).Count(operation => operation.OperationType == OutboxOperationTypeEnum.UpsertTripMetadata).Should().Be(1);
        MockNotifier.Received(2).NotifyWorkEnqueued();
    }

    [Fact]
    public async Task ItShouldNotListAnotherAccountsTrips()
    {
        // Arrange
        await TripStore.SaveAsync(OwnerLease, NewTrip(), 0, CancellationToken.None);
        // Act
        var other = await TripStore.ListAsync(OtherUserId, CancellationToken.None);
        // Assert
        other.Should().BeEmpty();
    }
}
