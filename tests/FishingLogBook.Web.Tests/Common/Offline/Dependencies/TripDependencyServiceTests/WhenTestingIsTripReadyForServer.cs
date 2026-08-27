using AwesomeAssertions;
using FishingLogBook.Web.Common;

namespace FishingLogBook.Web.Tests.Common.Offline.Dependencies.TripDependencyServiceTests;

public class WhenTestingIsTripReadyForServer : BaseTripDependencyServiceTest
{
    [Fact]
    public async Task ItShouldNotBeReadyWhenTheOwnerIsUnknown()
    {
        // Arrange
        await GivenTripAsync(TripId, SyncStatus.Synchronised);

        // Act
        var ready = await Sut.IsTripReadyForServerAsync(Guid.Empty, TripId, CancellationToken.None);

        // Assert
        ready.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotBeReadyWhenTheTripIsStillSavedLocally()
    {
        // Arrange
        await GivenTripAsync(TripId, SyncStatus.SavedLocally);

        // Act
        var ready = await Sut.IsTripReadyForServerAsync(OwnerUserId, TripId, CancellationToken.None);

        // Assert
        ready.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotBeReadyWhenTheTripFailedToSynchronise()
    {
        // Arrange
        await GivenTripAsync(TripId, SyncStatus.FailedToSynchronise);

        // Act
        var ready = await Sut.IsTripReadyForServerAsync(OwnerUserId, TripId, CancellationToken.None);

        // Assert
        ready.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotBeReadyWhenTheTripBelongsToAnotherAngler()
    {
        // Arrange
        await GivenTripAsync(TripId, SyncStatus.SavedLocally, OtherUserId);

        // Act
        var ready = await Sut.IsTripReadyForServerAsync(OtherUserId, TripId, CancellationToken.None);

        // Assert
        ready.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldBeReadyWhenTheTripIsNoLongerCachedLocally()
    {
        // Arrange

        // Act
        var ready = await Sut.IsTripReadyForServerAsync(OwnerUserId, TripId, CancellationToken.None);

        // Assert
        ready.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldBeReadyWhenAnotherAnglerOwnsTheCachedTrip()
    {
        // Arrange
        await GivenTripAsync(TripId, SyncStatus.SavedLocally, OtherUserId);

        // Act
        var ready = await Sut.IsTripReadyForServerAsync(OwnerUserId, TripId, CancellationToken.None);

        // Assert
        ready.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldBeReadyWhenTheTripHasSynchronised()
    {
        // Arrange
        await GivenTripAsync(TripId, SyncStatus.Synchronised);

        // Act
        var ready = await Sut.IsTripReadyForServerAsync(OwnerUserId, TripId, CancellationToken.None);

        // Assert
        ready.Should().BeTrue();
    }
}
