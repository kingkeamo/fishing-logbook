using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Offline.Stores.SqliteTripStoreTests;

public sealed class WhenTestingHydrateAndRevoke : BaseSqliteTripStoreTest
{
    [Fact]
    public async Task ItShouldRefuseToHydrateATripForAnotherAccount()
    {
        // Arrange
        var trip = NewTrip(userId: OtherUserId);
        // Act
        var act = () => TripStore.HydrateAsync(OwnerLease, trip, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ItShouldStopHydratingWhenTheSessionEnded()
    {
        // Arrange
        RevokeSession();
        // Act
        var act = () => TripStore.HydrateAsync(OwnerLease, NewTrip(), CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ItShouldCacheAServerTripWithoutQueuingAnyWork()
    {
        // Arrange
        var trip = NewTrip();
        // Act
        await TripStore.HydrateAsync(OwnerLease, trip, CancellationToken.None);
        // Assert
        var stored = await TripStore.GetAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None);
        stored!.HasPendingMetadata.Should().BeFalse();
        (await PendingAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRefreshACachedTripThatHasNoLocalEdits()
    {
        // Arrange
        var trip = NewTrip();
        await TripStore.HydrateAsync(OwnerLease, trip, CancellationToken.None);
        // Act
        await TripStore.HydrateAsync(OwnerLease, trip with { Detail = trip.Detail with { Trip = trip.Detail.Trip with { Title = "Server title" } } }, CancellationToken.None);
        // Assert
        (await TripStore.GetAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None))!.Detail.Trip.Title.Should().Be("Server title");
    }

    [Fact]
    public async Task ItShouldNeverOverwriteALocalEditThatHasNotSynchronisedYet()
    {
        // Arrange
        var trip = NewTrip();
        await TripStore.SaveAsync(OwnerLease, trip with { Detail = trip.Detail with { Trip = trip.Detail.Trip with { Title = "Local edit" } } }, 0, CancellationToken.None);
        // Act
        await TripStore.HydrateAsync(OwnerLease, trip with { Detail = trip.Detail with { Trip = trip.Detail.Trip with { Title = "Server title" } } }, CancellationToken.None);
        // Assert
        (await TripStore.GetAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None))!.Detail.Trip.Title.Should().Be("Local edit");
    }

    [Fact]
    public async Task ItShouldMarkACachedTripAsRevokedAndLeaveOthersAlone()
    {
        // Arrange
        var revoked = NewTrip();
        var kept = NewTrip();
        await TripStore.HydrateAsync(OwnerLease, revoked, CancellationToken.None);
        await TripStore.HydrateAsync(OwnerLease, kept, CancellationToken.None);
        // Act
        await TripStore.RevokeAsync(OwnerLease, revoked.Detail.Trip.Id, CancellationToken.None);
        // Assert
        (await TripStore.GetAsync(OwnerUserId, revoked.Detail.Trip.Id, CancellationToken.None))!.AccessRevoked.Should().BeTrue();
        (await TripStore.GetAsync(OwnerUserId, kept.Detail.Trip.Id, CancellationToken.None))!.AccessRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldDoNothingWhenRevokingATripThatIsNotCached()
    {
        // Arrange
        // Act
        await TripStore.RevokeAsync(OwnerLease, Guid.NewGuid(), CancellationToken.None);
        // Assert
        (await TripStore.ListAsync(OwnerUserId, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldStopRevokingWhenTheSessionEnded()
    {
        // Arrange
        RevokeSession();
        // Act
        var act = () => TripStore.RevokeAsync(OwnerLease, Guid.NewGuid(), CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
