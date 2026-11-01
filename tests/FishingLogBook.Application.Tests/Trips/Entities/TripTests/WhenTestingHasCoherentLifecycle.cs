using AwesomeAssertions;
using FishingLogBook.Domain.Enums;

namespace FishingLogBook.Application.Tests.Trips.Entities.TripTests;

public class WhenTestingHasCoherentLifecycle : BaseTripTest
{
    [Theory]
    [InlineData(TripStatusEnum.Planned)]
    [InlineData(TripStatusEnum.Cancelled)]
    public void ItShouldRequireAPlannedStartForAPlannedOrCancelledTrip(TripStatusEnum status)
    {
        // Arrange
        var withPlan = NewTrip(status, PlannedStartOn, PlannedStartOn);
        var withoutPlan = NewTrip(status, PlannedStartOn);

        // Act
        var coherentWithPlan = withPlan.HasCoherentLifecycle();
        var coherentWithoutPlan = withoutPlan.HasCoherentLifecycle();

        // Assert
        coherentWithPlan.Should().BeTrue();
        coherentWithoutPlan.Should().BeFalse();
    }

    [Theory]
    [InlineData(TripStatusEnum.Planned)]
    [InlineData(TripStatusEnum.Cancelled)]
    public void ItShouldRejectAnEndOnAPlannedOrCancelledTrip(TripStatusEnum status)
    {
        // Arrange
        var trip = NewTrip(status, PlannedStartOn, PlannedStartOn, endedOn: PlannedStartOn.AddHours(1));

        // Act
        var result = trip.HasCoherentLifecycle();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ItShouldRejectADefaultPlannedStart()
    {
        // Arrange
        var trip = NewTrip(TripStatusEnum.Planned, PlannedStartOn, default(DateTimeOffset));

        // Act
        var result = trip.HasCoherentLifecycle();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ItShouldKeepTheExistingRulesForActiveAndCompletedTrips()
    {
        // Arrange
        var active = NewTrip(TripStatusEnum.Active);
        var activeWithEnd = NewTrip(TripStatusEnum.Active, endedOn: StartedOn.AddHours(1));
        var completed = NewTrip(TripStatusEnum.Completed, endedOn: StartedOn.AddHours(1));
        var endsBeforeStart = NewTrip(TripStatusEnum.Completed, endedOn: StartedOn.AddHours(-1));

        // Act
        var results = new[]
        {
            active.HasCoherentLifecycle(),
            activeWithEnd.HasCoherentLifecycle(),
            completed.HasCoherentLifecycle(),
            endsBeforeStart.HasCoherentLifecycle()
        };

        // Assert
        results.Should().Equal(true, false, true, false);
    }
}
