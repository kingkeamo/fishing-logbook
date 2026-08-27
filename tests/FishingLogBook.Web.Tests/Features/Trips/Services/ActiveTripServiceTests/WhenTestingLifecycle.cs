using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Trips.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.ActiveTripServiceTests;

public class WhenTestingLifecycle : BaseActiveTripServiceTest
{
    [Fact]
    public async Task ItShouldRejectAStartWithNoOwner()
    {
        // Arrange
        // Act
        var act = () => Sut.StartAsync(Guid.Empty, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await MockTripStore.DidNotReceive().SaveAsync(
            Arg.Any<TripModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNoActiveTripForAnEmptyOwner()
    {
        // Arrange
        // Act
        var active = await Sut.GetActiveAsync(Guid.Empty, CancellationToken.None);

        // Assert
        active.Should().BeNull();
        await MockTripStore.DidNotReceive().GetActiveAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStartATripWithAClientIdentifierAndActiveStatus()
    {
        // Arrange
        // Act
        var started = await Sut.StartAsync(OwnerUserId, CancellationToken.None);

        // Assert
        started.Id.Should().NotBe(Guid.Empty);
        started.OwnerUserId.Should().Be(OwnerUserId);
        started.Status.Should().Be(TripConstants.Active);
        started.EndedOn.Should().BeNull();
        started.Title.Should().BeNull();
        started.PlaceName.Should().BeNull();
        started.Location.Should().BeNull();
        started.SyncStatus.Should().Be(SyncStatus.SavedLocally);
        started.StartedOn.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        await MockTripStore.Received(1).SaveAsync(
            Arg.Is<TripModel>(trip =>
                trip.Id == started.Id
                && trip.OwnerUserId == OwnerUserId
                && trip.Status == TripConstants.Active
                && trip.EndedOn == null
                && trip.Location == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldGiveEachStartedTripADistinctIdentifier()
    {
        // Arrange
        var first = await Sut.StartAsync(OwnerUserId, CancellationToken.None);
        Sut.Invalidate();

        // Act
        var second = await Sut.StartAsync(OwnerUserId, CancellationToken.None);

        // Assert
        second.Id.Should().NotBe(first.Id);
    }

    [Fact]
    public async Task ItShouldNotCarryLocationFromAPreviousTripIntoANewOne()
    {
        // Arrange
        MockLocationService.TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(CapturedLocation());
        var previous = ActiveTrip(location: new TripLocationModel(
            1, 2, 3, StartedOn, LocationDefaults.DeviceGps, LocationDefaults.Private, LocationDefaults.ConsentVersion));
        MockTripStore.GetAsync(OwnerUserId, previous.Id, Arg.Any<CancellationToken>()).Returns(previous);

        // Act
        var started = await Sut.StartAsync(OwnerUserId, CancellationToken.None);

        // Assert
        started.Location.Should().BeNull();
        await MockLocationService.DidNotReceive().TryCaptureAsync(
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRaiseStateChangedWhenATripStarts()
    {
        // Arrange
        var raised = 0;
        Sut.StateChanged += (_, _) => raised += 1;

        // Act
        await Sut.StartAsync(OwnerUserId, CancellationToken.None);

        // Assert
        raised.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldFinishTheSameTripWithACompletedStatusAndEndTime()
    {
        // Arrange
        var trip = ActiveTrip();

        // Act
        var finished = await Sut.FinishAsync(trip, CancellationToken.None);

        // Assert
        finished.Id.Should().Be(trip.Id);
        finished.Status.Should().Be(TripConstants.Completed);
        finished.EndedOn.Should().NotBeNull();
        finished.EndedOn!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        finished.StartedOn.Should().Be(trip.StartedOn);
        await MockTripStore.Received(1).SaveAsync(
            Arg.Is<TripModel>(saved =>
                saved.Id == trip.Id
                && saved.Status == TripConstants.Completed
                && saved.EndedOn != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportNoActiveTripAfterFinishing()
    {
        // Arrange
        var trip = ActiveTrip();
        MockTripStore.GetActiveAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns(trip);
        (await Sut.GetActiveAsync(OwnerUserId, CancellationToken.None)).Should().NotBeNull();

        // Act
        await Sut.FinishAsync(trip, CancellationToken.None);

        // Assert
        var active = await Sut.GetActiveAsync(OwnerUserId, CancellationToken.None);
        active.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldRaiseStateChangedWhenATripFinishes()
    {
        // Arrange
        var raised = 0;
        Sut.StateChanged += (_, _) => raised += 1;

        // Act
        await Sut.FinishAsync(ActiveTrip(), CancellationToken.None);

        // Assert
        raised.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldReadTheActiveTripFromTheStoreRatherThanMemory()
    {
        // Arrange
        MockTripStore.GetActiveAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns(ActiveTrip());

        // Act
        var active = await Sut.GetActiveAsync(OwnerUserId, CancellationToken.None);

        // Assert
        active!.Id.Should().Be(TripId);
        await MockTripStore.Received(1).GetActiveAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRereadTheStoreForRepeatedLookupsOfTheSameOwner()
    {
        // Arrange
        MockTripStore.GetActiveAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns(ActiveTrip());

        // Act
        await Sut.GetActiveAsync(OwnerUserId, CancellationToken.None);
        await Sut.GetActiveAsync(OwnerUserId, CancellationToken.None);
        await Sut.GetActiveAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripStore.Received(1).GetActiveAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReadAgainForADifferentOwner()
    {
        // Arrange
        MockTripStore.GetActiveAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns(ActiveTrip());
        MockTripStore.GetActiveAsync(OtherUserId, Arg.Any<CancellationToken>()).Returns((TripModel?)null);
        await Sut.GetActiveAsync(OwnerUserId, CancellationToken.None);

        // Act
        var other = await Sut.GetActiveAsync(OtherUserId, CancellationToken.None);

        // Assert
        other.Should().BeNull();
        await MockTripStore.Received(1).GetActiveAsync(OtherUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReadTheStoreAgainAfterBeingInvalidated()
    {
        // Arrange
        MockTripStore.GetActiveAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns(ActiveTrip());
        await Sut.GetActiveAsync(OwnerUserId, CancellationToken.None);

        // Act
        Sut.Invalidate();
        await Sut.GetActiveAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripStore.Received(2).GetActiveAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSurfaceAStoreFailureFromTheActiveLookup()
    {
        // Arrange
        MockTripStore.GetActiveAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("active-read timed out."));

        // Act
        var act = () => Sut.GetActiveAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
    }
}
