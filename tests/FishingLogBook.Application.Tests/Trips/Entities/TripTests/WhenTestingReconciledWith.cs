using AwesomeAssertions;
using FishingLogBook.Domain.Enums;

namespace FishingLogBook.Application.Tests.Trips.Entities.TripTests;

public class WhenTestingReconciledWith : BaseTripTest
{
    [Fact]
    public void ItShouldKeepTheIncomingPlanWhenThePlanIsEdited()
    {
        // Arrange
        var existing = NewTrip(TripStatusEnum.Planned, PlannedStartOn, PlannedStartOn);
        var incoming = NewTrip(TripStatusEnum.Planned, PlannedStartOn.AddHours(2), PlannedStartOn.AddHours(2));

        // Act
        var result = incoming.ReconciledWith(existing);

        // Assert
        result.PlannedStartOn.Should().Be(PlannedStartOn.AddHours(2));
        result.StartedOn.Should().Be(PlannedStartOn.AddHours(2));
    }

    [Fact]
    public void ItShouldRecordTheActualStartAndKeepTheStoredPlanOnActivation()
    {
        // Arrange
        var existing = NewTrip(TripStatusEnum.Planned, PlannedStartOn, PlannedStartOn);
        var incoming = NewTrip(TripStatusEnum.Active, StartedOn, PlannedStartOn.AddDays(9), title: "Corrib");

        // Act
        var result = incoming.ReconciledWith(existing);

        // Assert
        result.Status.Should().Be(TripStatusEnum.Active);
        result.StartedOn.Should().Be(StartedOn);
        result.PlannedStartOn.Should().Be(PlannedStartOn);
        result.Title.Should().Be("Corrib");
    }

    [Fact]
    public void ItShouldKeepTheFirstActualStartWhenAnActivatedTripIsActivatedAgain()
    {
        // Arrange
        var existing = NewTrip(TripStatusEnum.Active, StartedOn, PlannedStartOn);
        var incoming = NewTrip(TripStatusEnum.Active, StartedOn.AddMinutes(30), PlannedStartOn);

        // Act
        var result = incoming.ReconciledWith(existing);

        // Assert
        result.StartedOn.Should().Be(StartedOn);
        result.PlannedStartOn.Should().Be(PlannedStartOn);
    }

    [Fact]
    public void ItShouldLetAnUnplannedActiveTripKeepAnEditedStart()
    {
        // Arrange
        var existing = NewTrip(TripStatusEnum.Active, StartedOn);
        var incoming = NewTrip(TripStatusEnum.Active, StartedOn.AddMinutes(5));

        // Act
        var result = incoming.ReconciledWith(existing);

        // Assert
        result.StartedOn.Should().Be(StartedOn.AddMinutes(5));
        result.PlannedStartOn.Should().BeNull();
    }

    [Fact]
    public void ItShouldKeepThePlanWhenAnActivatedTripIsCompleted()
    {
        // Arrange
        var existing = NewTrip(TripStatusEnum.Active, StartedOn, PlannedStartOn);
        var incoming = NewTrip(TripStatusEnum.Completed, StartedOn, endedOn: StartedOn.AddHours(4));

        // Act
        var result = incoming.ReconciledWith(existing);

        // Assert
        result.Status.Should().Be(TripStatusEnum.Completed);
        result.PlannedStartOn.Should().Be(PlannedStartOn);
        result.EndedOn.Should().Be(StartedOn.AddHours(4));
    }

    [Fact]
    public void ItShouldKeepTheStoredPlanWhenAPlannedTripIsCancelled()
    {
        // Arrange
        var existing = NewTrip(TripStatusEnum.Planned, PlannedStartOn, PlannedStartOn);
        var incoming = NewTrip(TripStatusEnum.Cancelled, PlannedStartOn.AddDays(2), PlannedStartOn.AddDays(2));

        // Act
        var result = incoming.ReconciledWith(existing);

        // Assert
        result.Status.Should().Be(TripStatusEnum.Cancelled);
        result.PlannedStartOn.Should().Be(PlannedStartOn);
    }
}
