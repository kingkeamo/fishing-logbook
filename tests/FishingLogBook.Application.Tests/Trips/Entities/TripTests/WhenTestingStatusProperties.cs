using AwesomeAssertions;
using FishingLogBook.Domain.Enums;

namespace FishingLogBook.Application.Tests.Trips.Entities.TripTests;

public class WhenTestingStatusProperties : BaseTripTest
{
    [Theory]
    [InlineData(TripStatusEnum.Active, true, false, false, true)]
    [InlineData(TripStatusEnum.Completed, false, false, false, true)]
    [InlineData(TripStatusEnum.Planned, false, true, false, false)]
    [InlineData(TripStatusEnum.Cancelled, false, false, true, false)]
    public void ItShouldDescribeEachStatus(
        TripStatusEnum status,
        bool active,
        bool planned,
        bool cancelled,
        bool acceptsContributions)
    {
        // Arrange
        var trip = NewTrip(status);

        // Act
        var flags = (trip.IsActive, trip.IsPlanned, trip.IsCancelled, trip.AcceptsContributions);

        // Assert
        flags.Should().Be((active, planned, cancelled, acceptsContributions));
    }

    [Fact]
    public void ItShouldKeepThePlanWhenATripIsCompletedAutomatically()
    {
        // Arrange
        var trip = NewTrip(TripStatusEnum.Active, StartedOn, PlannedStartOn);

        // Act
        var completed = trip.CompletedAt(StartedOn.AddHours(2));

        // Assert
        completed.Status.Should().Be(TripStatusEnum.Completed);
        completed.PlannedStartOn.Should().Be(PlannedStartOn);
        completed.EndedOn.Should().Be(StartedOn.AddHours(2));
    }

    [Fact]
    public void ItShouldNeverEndATripBeforeItStarted()
    {
        // Arrange
        var trip = NewTrip(TripStatusEnum.Active, StartedOn);

        // Act
        var completed = trip.CompletedAt(StartedOn.AddHours(-3));

        // Assert
        completed.EndedOn.Should().Be(StartedOn);
    }
}
