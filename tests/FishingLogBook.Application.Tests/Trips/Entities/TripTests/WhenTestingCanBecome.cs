using AwesomeAssertions;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;

namespace FishingLogBook.Application.Tests.Trips.Entities.TripTests;

public class WhenTestingCanBecome : BaseTripTest
{
    [Theory]
    [InlineData(TripStatusEnum.Planned, TripStatusEnum.Planned, true)]
    [InlineData(TripStatusEnum.Planned, TripStatusEnum.Active, true)]
    [InlineData(TripStatusEnum.Planned, TripStatusEnum.Cancelled, true)]
    [InlineData(TripStatusEnum.Planned, TripStatusEnum.Completed, false)]
    [InlineData(TripStatusEnum.Cancelled, TripStatusEnum.Cancelled, true)]
    [InlineData(TripStatusEnum.Cancelled, TripStatusEnum.Planned, false)]
    [InlineData(TripStatusEnum.Cancelled, TripStatusEnum.Active, false)]
    [InlineData(TripStatusEnum.Cancelled, TripStatusEnum.Completed, false)]
    [InlineData(TripStatusEnum.Active, TripStatusEnum.Active, true)]
    [InlineData(TripStatusEnum.Active, TripStatusEnum.Completed, true)]
    [InlineData(TripStatusEnum.Active, TripStatusEnum.Planned, false)]
    [InlineData(TripStatusEnum.Active, TripStatusEnum.Cancelled, false)]
    [InlineData(TripStatusEnum.Completed, TripStatusEnum.Completed, true)]
    [InlineData(TripStatusEnum.Completed, TripStatusEnum.Active, true)]
    [InlineData(TripStatusEnum.Completed, TripStatusEnum.Planned, false)]
    [InlineData(TripStatusEnum.Completed, TripStatusEnum.Cancelled, false)]
    public void ItShouldAllowOnlyTheSupportedLifecycleTransitions(
        TripStatusEnum current,
        TripStatusEnum next,
        bool allowed)
    {
        // Arrange
        var trip = NewTrip(current);

        // Act
        var result = trip.CanBecome(next);

        // Assert
        result.Should().Be(allowed);
    }

    [Theory]
    [InlineData(TripStatusEnum.Planned, true)]
    [InlineData(TripStatusEnum.Active, true)]
    [InlineData(TripStatusEnum.Completed, true)]
    [InlineData(TripStatusEnum.Cancelled, false)]
    public void ItShouldAllowANewTripToStartInEveryStatusExceptCancelled(TripStatusEnum status, bool allowed)
    {
        // Arrange
        // Act
        var result = Trip.CanBeCreatedAs(status);

        // Assert
        result.Should().Be(allowed);
    }
}
