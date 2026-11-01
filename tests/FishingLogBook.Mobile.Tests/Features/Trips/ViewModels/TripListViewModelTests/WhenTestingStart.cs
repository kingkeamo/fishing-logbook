using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Profile.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripListViewModelTests;

public sealed class WhenTestingStart : BaseTripListViewModelTest
{
    [Fact]
    public async Task ItShouldNotStartWhileAnOwnedTripIsAlreadyActive()
    {
        // Arrange
        ServeHistory(trips: Summary(Guid.NewGuid()));
        await Sut.LoadAsync(CancellationToken.None);
        // Act
        var id = await Sut.StartAsync(CancellationToken.None);
        // Assert
        id.Should().BeNull();
        await MockTrips.DidNotReceive().StartAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReloadAndReturnNothingWhenAnotherTripBecameActiveFirst()
    {
        // Arrange
        ServeHistory();
        await Sut.LoadAsync(CancellationToken.None);
        MockTrips.StartAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<FishingLogBook.Mobile.Features.Trips.Offline.Models.LocalTripRecordModel>(_ => throw new InvalidOperationException());
        // Act
        var id = await Sut.StartAsync(CancellationToken.None);
        // Assert
        id.Should().BeNull();
        await MockTrips.Received(2).LoadAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStartWithoutAPlaceWhenThereIsNoDefaultLocation()
    {
        // Arrange
        var tripId = Guid.NewGuid();
        ServeHistory();
        await Sut.LoadAsync(CancellationToken.None);
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns((AnglerPreferencesModel?)null);
        MockTrips.StartAsync(null, null, Arg.Any<CancellationToken>()).Returns(Started(tripId));
        // Act
        var id = await Sut.StartAsync(CancellationToken.None);
        // Assert
        id.Should().Be(tripId);
        await MockTrips.Received(1).StartAsync(null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStartATripAtTheDefaultFishingLocationAndReturnItsId()
    {
        // Arrange
        var tripId = Guid.NewGuid();
        ServeHistory();
        await Sut.LoadAsync(CancellationToken.None);
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(PreferencesWithDefaultLocation("Lough Corrib"));
        MockTrips.StartAsync(null, "Lough Corrib", Arg.Any<CancellationToken>()).Returns(Started(tripId));
        // Act
        var id = await Sut.StartAsync(CancellationToken.None);
        // Assert
        id.Should().Be(tripId);
        await MockTrips.Received(1).StartAsync(null, "Lough Corrib", Arg.Any<CancellationToken>());
        Sut.CanInteract.Should().BeTrue();
    }
}
