using AwesomeAssertions;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Shared.Tests.Constants.TripConstantsTests;

public class WhenTestingTimeHelpers
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-12T12:00:00Z");

    [Fact]
    public void ItShouldRejectADefaultStartTime()
    {
        // Arrange
        // Act
        var result = TripConstants.IsStartedOnValid(default, Now);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ItShouldAcceptAStartTimeUpToNowAndRejectTheSmallestIncrementBeyondIt()
    {
        // Arrange
        // Act
        var atNow = TripConstants.IsStartedOnValid(Now, Now);
        var justAfter = TripConstants.IsStartedOnValid(Now.AddTicks(1), Now);

        // Assert
        atNow.Should().BeTrue();
        justAfter.Should().BeFalse();
    }

    [Fact]
    public void ItShouldAcceptAnOpenEndedTrip()
    {
        // Arrange
        // Act
        var result = TripConstants.IsEndedOnValid(Now.AddHours(-2), null, Now);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ItShouldAcceptAnEndTimeFromTheStartUpToNow()
    {
        // Arrange
        var startedOn = Now.AddHours(-2);

        // Act
        var atStart = TripConstants.IsEndedOnValid(startedOn, startedOn, Now);
        var atNow = TripConstants.IsEndedOnValid(startedOn, Now, Now);

        // Assert
        atStart.Should().BeTrue();
        atNow.Should().BeTrue();
    }

    [Fact]
    public void ItShouldRejectAnEndTimeBeforeTheStartOrAfterNow()
    {
        // Arrange
        var startedOn = Now.AddHours(-2);

        // Act
        var beforeStart = TripConstants.IsEndedOnValid(startedOn, startedOn.AddTicks(-1), Now);
        var afterNow = TripConstants.IsEndedOnValid(startedOn, Now.AddTicks(1), Now);

        // Assert
        beforeStart.Should().BeFalse();
        afterNow.Should().BeFalse();
    }
}
