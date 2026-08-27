using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Trips.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.ActiveTripServiceTests;

public class WhenTestingTryAttachLocation : BaseActiveTripServiceTest
{
    [Fact]
    public async Task ItShouldNotCaptureWhenTheTripAlreadyHasALocation()
    {
        // Arrange
        var trip = ActiveTrip(location: new TripLocationModel(
            1, 2, 3, StartedOn, LocationDefaults.DeviceGps, LocationDefaults.Private, LocationDefaults.ConsentVersion));

        // Act
        var located = await Sut.TryAttachLocationAsync(trip, CancellationToken.None);

        // Assert
        located.Should().BeNull();
        await MockLocationService.DidNotReceive().TryCaptureAsync(
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
        await MockTripStore.DidNotReceive().SaveAsync(
            Arg.Any<TripModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotCaptureForACompletedTrip()
    {
        // Arrange
        var trip = ActiveTrip() with { Status = TripConstants.Completed, EndedOn = StartedOn.AddHours(2) };

        // Act
        var located = await Sut.TryAttachLocationAsync(trip, CancellationToken.None);

        // Assert
        located.Should().BeNull();
        await MockLocationService.DidNotReceive().TryCaptureAsync(
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotPersistAnythingWhenLocationIsUnavailable()
    {
        // Arrange
        MockLocationService.TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((CatchLocationModel?)null);

        // Act
        var located = await Sut.TryAttachLocationAsync(ActiveTrip(), CancellationToken.None);

        // Assert
        located.Should().BeNull();
        await MockTripStore.DidNotReceive().SaveAsync(
            Arg.Any<TripModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotPersistAnythingWhenLocationCaptureThrows()
    {
        // Arrange
        MockLocationService.TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Geolocation is unavailable."));

        // Act
        var located = await Sut.TryAttachLocationAsync(ActiveTrip(), CancellationToken.None);

        // Assert
        located.Should().BeNull();
        await MockTripStore.DidNotReceive().SaveAsync(
            Arg.Any<TripModel>(),
            Arg.Any<CancellationToken>());
        await MockLogging.Received(1).LogErrorAsync(
            "capturing a trip location",
            Arg.Any<InvalidOperationException>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotAttachToATripThatFinishedWhileCapturing()
    {
        // Arrange
        MockLocationService.TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(CapturedLocation());
        var trip = ActiveTrip();
        MockTripStore.GetAsync(OwnerUserId, trip.Id, Arg.Any<CancellationToken>())
            .Returns(trip with { Status = TripConstants.Completed, EndedOn = StartedOn.AddHours(1) });

        // Act
        var located = await Sut.TryAttachLocationAsync(trip, CancellationToken.None);

        // Assert
        located.Should().BeNull();
        await MockTripStore.DidNotReceive().SaveAsync(
            Arg.Any<TripModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotOverwriteALocationSavedWhileCapturing()
    {
        // Arrange
        MockLocationService.TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(CapturedLocation());
        var trip = ActiveTrip();
        MockTripStore.GetAsync(OwnerUserId, trip.Id, Arg.Any<CancellationToken>())
            .Returns(trip with
            {
                Location = new TripLocationModel(
                    10, 20, 5, StartedOn, LocationDefaults.DeviceGps, LocationDefaults.Private, LocationDefaults.ConsentVersion)
            });

        // Act
        var located = await Sut.TryAttachLocationAsync(trip, CancellationToken.None);

        // Assert
        located.Should().BeNull();
        await MockTripStore.DidNotReceive().SaveAsync(
            Arg.Any<TripModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotAttachToATripThatIsNoLongerStored()
    {
        // Arrange
        MockLocationService.TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(CapturedLocation());
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns((TripModel?)null);

        // Act
        var located = await Sut.TryAttachLocationAsync(ActiveTrip(), CancellationToken.None);

        // Assert
        located.Should().BeNull();
        await MockTripStore.DidNotReceive().SaveAsync(
            Arg.Any<TripModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistACapturedPrivateLocationWithItsProvenance()
    {
        // Arrange
        MockLocationService.TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(CapturedLocation());
        var trip = ActiveTrip();
        MockTripStore.GetAsync(OwnerUserId, trip.Id, Arg.Any<CancellationToken>()).Returns(trip);

        // Act
        var located = await Sut.TryAttachLocationAsync(trip, CancellationToken.None);

        // Assert
        located.Should().NotBeNull();
        located!.Location!.Latitude.Should().Be(53.4419);
        located.Location.Longitude.Should().Be(-9.2531);
        located.Location.AccuracyMetres.Should().Be(8);
        located.Location.Source.Should().Be(LocationDefaults.DeviceGps);
        located.Location.Visibility.Should().Be(LocationDefaults.Private);
        located.Location.ConsentVersion.Should().Be(LocationDefaults.ConsentVersion);
        await MockTripStore.Received(1).SaveAsync(
            Arg.Is<TripModel>(saved =>
                saved.Id == trip.Id
                && saved.Location != null
                && saved.Location.Visibility == LocationDefaults.Private),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCaptureOpportunisticallyRatherThanAsAUserRequest()
    {
        // Arrange
        MockLocationService.TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(CapturedLocation());
        var trip = ActiveTrip();
        MockTripStore.GetAsync(OwnerUserId, trip.Id, Arg.Any<CancellationToken>()).Returns(trip);

        // Act
        await Sut.TryAttachLocationAsync(trip, CancellationToken.None);

        // Assert
        await MockLocationService.Received(1).TryCaptureAsync(false, Arg.Any<CancellationToken>());
    }
}
