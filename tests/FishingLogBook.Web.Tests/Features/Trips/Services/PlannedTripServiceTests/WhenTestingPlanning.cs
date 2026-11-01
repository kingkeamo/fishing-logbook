using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Models;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.PlannedTripServiceTests;

public class WhenTestingPlanning : BasePlannedTripServiceTest
{
    [Fact]
    public async Task ItShouldRejectPlanningWithoutAnOwner()
    {
        // Arrange
        var plannedStart = Now.AddDays(1);

        // Act
        var act = () => Sut.PlanAsync(Guid.Empty, "Corrib", null, plannedStart, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await MockTripStore.DidNotReceive().SaveAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotUpdateATripThatIsNotPlanned()
    {
        // Arrange
        var active = PlannedTrip(Now) with { Status = TripConstants.Active };
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(active);

        // Act
        var result = await Sut.UpdatePlanAsync(OwnerUserId, TripId, "New", null, Now.AddDays(1), CancellationToken.None);

        // Assert
        result.Should().BeNull();
        await MockTripStore.DidNotReceive().SaveAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotUpdateAPlannedTripOwnedBySomeoneElse()
    {
        // Arrange
        MockTripStore.GetAsync(GuestUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(PlannedTrip(Now, ownerUserId: OwnerUserId));

        // Act
        var result = await Sut.UpdatePlanAsync(GuestUserId, TripId, "New", null, Now.AddDays(1), CancellationToken.None);

        // Assert
        result.Should().BeNull();
        await MockTripStore.DidNotReceive().SaveAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotCancelATripThatIsNotPlanned()
    {
        // Arrange
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(PlannedTrip(Now) with { Status = TripConstants.Completed });

        // Act
        var result = await Sut.CancelPlanAsync(OwnerUserId, TripId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        await MockTripStore.DidNotReceive().SaveAsync(Arg.Any<TripModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSavePlannedTripsWithTheirPlannedStartAsTheStart()
    {
        // Arrange
        var plannedStart = Now.AddDays(2);

        // Act
        var result = await Sut.PlanAsync(OwnerUserId, "  Corrib  ", " Shore ", plannedStart, CancellationToken.None);

        // Assert
        result.Status.Should().Be(TripConstants.Planned);
        result.StartedOn.Should().Be(plannedStart);
        result.PlannedStartOn.Should().Be(plannedStart);
        result.Title.Should().Be("Corrib");
        result.PlaceName.Should().Be("Shore");
        result.SyncStatus.Should().Be(SyncStatus.SavedLocally);
        await MockTripStore.Received(1).SaveAsync(
            Arg.Is<TripModel>(trip => trip.Id == result.Id
                && trip.OwnerUserId == OwnerUserId
                && trip.Status == TripConstants.Planned
                && trip.PlannedStartOn == plannedStart),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldUpdateThePlannedStartAndMarkTheTripForSync()
    {
        // Arrange
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(PlannedTrip(Now));
        var newStart = Now.AddDays(1);

        // Act
        var result = await Sut.UpdatePlanAsync(OwnerUserId, TripId, "Renamed", null, newStart, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("Renamed");
        result.StartedOn.Should().Be(newStart);
        result.PlannedStartOn.Should().Be(newStart);
        result.SyncStatus.Should().Be(SyncStatus.SavedLocally);
        await MockTripStore.Received(1).SaveAsync(
            Arg.Is<TripModel>(trip => trip.Id == TripId && trip.PlannedStartOn == newStart),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCancelAPlannedTripAndMarkItForSync()
    {
        // Arrange
        MockTripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(PlannedTrip(Now));

        // Act
        var result = await Sut.CancelPlanAsync(OwnerUserId, TripId, CancellationToken.None);

        // Assert
        result!.Status.Should().Be(TripConstants.Cancelled);
        result.PlannedStartOn.Should().Be(Now);
        await MockTripStore.Received(1).SaveAsync(
            Arg.Is<TripModel>(trip => trip.Status == TripConstants.Cancelled
                && trip.SyncStatus == SyncStatus.SavedLocally),
            Arg.Any<CancellationToken>());
    }
}
