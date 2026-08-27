using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Trips.Models;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.ActiveTripServiceTests;

public class WhenTestingUpdateDetails : BaseActiveTripServiceTest
{
    [Fact]
    public async Task ItShouldNotSaveWhenTheTripIsNoLongerStored()
    {
        // Arrange
        var trip = ActiveTrip();
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns((TripModel?)null);

        // Act
        var updated = await Sut.UpdateDetailsAsync(trip, null, "Lough Corrib", CancellationToken.None);

        // Assert
        updated.Should().BeNull();
        await MockTripStore.DidNotReceive().SaveAsync(
            Arg.Any<TripModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreAPlaceNameThatIsTooLong()
    {
        // Arrange
        var trip = ActiveTrip();
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(trip with { PlaceName = "Lough Corrib" });

        // Act
        var updated = await Sut.UpdateDetailsAsync(
            trip,
            null,
            new string('a', TripConstants.MaxPlaceNameLength + 1),
            CancellationToken.None);

        // Assert
        updated!.PlaceName.Should().BeNull();
        await MockTripStore.Received(1).SaveAsync(
            Arg.Is<TripModel>(saved => saved.PlaceName == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldClearThePlaceName()
    {
        // Arrange
        var trip = ActiveTrip();
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(trip with { PlaceName = "Lough Corrib", SyncStatus = SyncStatus.Synchronised });

        // Act
        var updated = await Sut.UpdateDetailsAsync(trip, null, null, CancellationToken.None);

        // Assert
        updated!.PlaceName.Should().BeNull();
        updated.SyncStatus.Should().Be(SyncStatus.SavedLocally);
        await MockTripStore.Received(1).SaveAsync(
            Arg.Is<TripModel>(saved =>
                saved.Id == TripId &&
                saved.PlaceName == null &&
                saved.SyncStatus == SyncStatus.SavedLocally),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheCapturedLocationWhenThePlaceNameChanges()
    {
        // Arrange
        var located = ActiveTrip(location: new TripLocationModel(
            53.4419,
            -9.2531,
            8,
            StartedOn,
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion));
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(located with { PlaceName = "Lough Corrib" });

        // Act
        var updated = await Sut.UpdateDetailsAsync(located, null, "River Moy", CancellationToken.None);

        // Assert
        updated!.PlaceName.Should().Be("River Moy");
        updated.Location.Should().NotBeNull();
        await MockTripStore.Received(1).SaveAsync(
            Arg.Is<TripModel>(saved => saved.PlaceName == "River Moy" && saved.Location != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldClearTheTitleWhenTheAnglerEmptiesIt()
    {
        // Arrange
        var trip = ActiveTrip();
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(trip with { Title = "Morning session", PlaceName = "Lough Corrib" });

        // Act
        var updated = await Sut.UpdateDetailsAsync(trip, "   ", "Lough Corrib", CancellationToken.None);

        // Assert
        updated!.Title.Should().BeNull();
        updated.PlaceName.Should().Be("Lough Corrib");
        await MockTripStore.Received(1).SaveAsync(
            Arg.Is<TripModel>(saved => saved.Title == null && saved.PlaceName == "Lough Corrib"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreATitleThatIsTooLong()
    {
        // Arrange
        var trip = ActiveTrip();
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(trip);

        // Act
        var updated = await Sut.UpdateDetailsAsync(
            trip,
            new string('a', TripConstants.MaxTitleLength + 1),
            "Lough Corrib",
            CancellationToken.None);

        // Assert
        updated!.Title.Should().BeNull();
        await MockTripStore.Received(1).SaveAsync(
            Arg.Is<TripModel>(saved => saved.Title == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveTheTrimmedTitleAndPlaceNameAgainstTheStoredTrip()
    {
        // Arrange
        var trip = ActiveTrip();
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(trip with { SyncStatus = SyncStatus.Synchronised });

        // Act
        var updated = await Sut.UpdateDetailsAsync(
            trip,
            "  Morning session  ",
            "  Lough Corrib  ",
            CancellationToken.None);

        // Assert
        updated!.PlaceName.Should().Be("Lough Corrib");
        updated.Title.Should().Be("Morning session");
        updated.SyncStatus.Should().Be(SyncStatus.SavedLocally);
        await MockTripStore.Received(1).GetAsync(
            OwnerUserId,
            TripId,
            Arg.Any<CancellationToken>());
        await MockTripStore.Received(1).SaveAsync(
            Arg.Is<TripModel>(saved =>
                saved.PlaceName == "Lough Corrib" &&
                saved.Title == "Morning session" &&
                saved.Status == TripConstants.Active),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotChangeTheSavedFishingLocationsWhenTheTripPlaceChanges()
    {
        // Arrange
        var trip = ActiveTrip();
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(trip with { PlaceName = "Lough Corrib" });

        // Act
        await Sut.UpdateDetailsAsync(trip, null, "Small lake near Clifden", CancellationToken.None);

        // Assert
        await MockAnglerPreferences.DidNotReceive().SetAsync(
            Arg.Any<Guid>(),
            Arg.Any<AnglerPreferencesModel>(),
            Arg.Any<CancellationToken>());
        await MockAnglerPreferences.DidNotReceive().GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldServeTheUpdatedPlaceAsTheCachedActiveTrip()
    {
        // Arrange
        var trip = ActiveTrip();
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(trip);

        // Act
        await Sut.UpdateDetailsAsync(trip, null, "Lough Corrib", CancellationToken.None);
        var active = await Sut.GetActiveAsync(OwnerUserId, CancellationToken.None);

        // Assert
        active!.PlaceName.Should().Be("Lough Corrib");
        await MockTripStore.DidNotReceive().GetActiveAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotCacheACompletedTripAsActive()
    {
        // Arrange
        var trip = ActiveTrip();
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(trip with { Status = TripConstants.Completed, EndedOn = StartedOn.AddHours(2) });

        // Act
        var updated = await Sut.UpdateDetailsAsync(trip, null, "Lough Corrib", CancellationToken.None);
        var active = await Sut.GetActiveAsync(OwnerUserId, CancellationToken.None);

        // Assert
        updated!.PlaceName.Should().Be("Lough Corrib");
        active.Should().BeNull();
        await MockTripStore.Received(1).GetActiveAsync(
            OwnerUserId,
            Arg.Any<CancellationToken>());
    }
}
