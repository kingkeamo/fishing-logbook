using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Models.LocalTripRecordModelTests;

public sealed class WhenTestingPlannedLifecycle
{
    [Theory]
    [InlineData(TripConstants.Planned)]
    [InlineData(TripConstants.Cancelled)]
    public void ItShouldLetTheOwnerManageButNotContributeToAPlannedOrCancelledTrip(string status)
    {
        // Arrange
        var trip = Trip(status);

        // Act
        var result = (trip.HasPlannedLifecycle, trip.CanManage, trip.CanContribute, trip.IsActive);

        // Assert
        result.Should().Be((true, true, false, false));
    }

    [Theory]
    [InlineData(TripConstants.Planned)]
    [InlineData(TripConstants.Cancelled)]
    public void ItShouldNotLetAParticipantManageOrContributeToAPlannedOrCancelledTrip(string status)
    {
        // Arrange
        var trip = Trip(status, TripParticipantConstants.Participant, owner: FriendId);

        // Act
        var result = (trip.CanManage, trip.CanContribute);

        // Assert
        result.Should().Be((false, false));
    }

    [Theory]
    [InlineData(TripConstants.Active)]
    [InlineData(TripConstants.Completed)]
    public void ItShouldLetAnOwnerContributeToAnActiveOrCompletedTrip(string status)
    {
        // Arrange
        var trip = Trip(status, endedOn: status == TripConstants.Completed ? Now : null);

        // Act
        var result = (trip.HasPlannedLifecycle, trip.CanManage, trip.CanContribute);

        // Assert
        result.Should().Be((false, true, true));
    }

    [Fact]
    public void ItShouldTellAPlannedTripFromACancelledOne()
    {
        // Arrange
        var planned = Trip(TripConstants.Planned);
        var cancelled = Trip(TripConstants.Cancelled);

        // Act
        var result = (planned.IsPlanned, planned.IsCancelled, cancelled.IsPlanned, cancelled.IsCancelled);

        // Assert
        result.Should().Be((true, false, false, true));
    }

    [Fact]
    public void ItShouldGiveARevokedOwnerNoAccessToAPlannedTrip()
    {
        // Arrange
        var trip = Trip(TripConstants.Planned) with { AccessRevoked = true };

        // Act
        var result = (trip.HasAccess, trip.CanManage);

        // Assert
        result.Should().Be((false, false));
    }
}
