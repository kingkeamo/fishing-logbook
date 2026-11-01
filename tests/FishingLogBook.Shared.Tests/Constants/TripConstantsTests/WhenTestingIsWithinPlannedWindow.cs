using AwesomeAssertions;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Shared.Tests.Constants.TripConstantsTests;

public class WhenTestingIsWithinPlannedWindow
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-31T23:30:00Z");

    [Fact]
    public void ItShouldIncludeAPlanExactlyThreeDaysAfterNow()
    {
        // Arrange
        var plannedStartOn = Now.AddDays(3);

        // Act
        var result = TripConstants.IsWithinPlannedWindow(plannedStartOn, Now, TripConstants.DefaultPlannedWindowDays);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ItShouldIncludeAPlanExactlyThreeDaysBeforeNow()
    {
        // Arrange
        var plannedStartOn = Now.AddDays(-3);

        // Act
        var result = TripConstants.IsWithinPlannedWindow(plannedStartOn, Now, TripConstants.DefaultPlannedWindowDays);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ItShouldExcludeAPlanTheSmallestIncrementBeyondThreeDaysAhead()
    {
        // Arrange
        var plannedStartOn = Now.AddDays(3).AddTicks(1);

        // Act
        var result = TripConstants.IsWithinPlannedWindow(plannedStartOn, Now, TripConstants.DefaultPlannedWindowDays);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ItShouldExcludeAPlanTheSmallestIncrementBeyondThreeDaysBehind()
    {
        // Arrange
        var plannedStartOn = Now.AddDays(-3).AddTicks(-1);

        // Act
        var result = TripConstants.IsWithinPlannedWindow(plannedStartOn, Now, TripConstants.DefaultPlannedWindowDays);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ItShouldDecideByTheInstantWhateverOffsetThePlanIsWrittenIn()
    {
        // Arrange
        var boundaryInSydney = new DateTimeOffset(2026, 11, 4, 10, 30, 0, TimeSpan.FromHours(11));
        var justBeyondInSydney = boundaryInSydney.AddTicks(1);
        var boundaryInHonolulu = new DateTimeOffset(2026, 11, 3, 13, 30, 0, TimeSpan.FromHours(-10));

        // Act
        var onBoundary = TripConstants.IsWithinPlannedWindow(boundaryInSydney, Now, 3);
        var beyond = TripConstants.IsWithinPlannedWindow(justBeyondInSydney, Now, 3);
        var sameInstantElsewhere = TripConstants.IsWithinPlannedWindow(boundaryInHonolulu, Now, 3);

        // Assert
        boundaryInSydney.UtcDateTime.Should().Be(boundaryInHonolulu.UtcDateTime);
        onBoundary.Should().BeTrue();
        beyond.Should().BeFalse();
        sameInstantElsewhere.Should().BeTrue();
    }

    [Fact]
    public void ItShouldTreatAPlanOnTheNextCalendarDayAsInsideWhenItIsWithinTheWindow()
    {
        // Arrange
        var justAfterMidnight = DateTimeOffset.Parse("2026-11-01T00:05:00Z");

        // Act
        var result = TripConstants.IsWithinPlannedWindow(justAfterMidnight, Now, 3);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ItShouldOnlyMatchTheExactInstantWhenTheWindowIsZeroDays()
    {
        // Arrange
        // Act
        var exact = TripConstants.IsWithinPlannedWindow(Now, Now, 0);
        var oneTickLater = TripConstants.IsWithinPlannedWindow(Now.AddTicks(1), Now, 0);

        // Assert
        exact.Should().BeTrue();
        oneTickLater.Should().BeFalse();
    }

    [Fact]
    public void ItShouldUseAWiderWindowWhenOneIsGiven()
    {
        // Arrange
        var plannedStartOn = Now.AddDays(5);

        // Act
        var narrow = TripConstants.IsWithinPlannedWindow(plannedStartOn, Now, 3);
        var wide = TripConstants.IsWithinPlannedWindow(plannedStartOn, Now, 7);

        // Assert
        narrow.Should().BeFalse();
        wide.Should().BeTrue();
    }
}
