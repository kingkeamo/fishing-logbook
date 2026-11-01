using AwesomeAssertions;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Shared.Tests.Constants.TripConstantsTests;

public class WhenTestingPlannedHelpers
{
    [Theory]
    [InlineData(null, 3)]
    [InlineData(-1, 3)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(30, 30)]
    [InlineData(31, 3)]
    public void ItShouldFallBackToTheDefaultWindowWhenTheRequestedOneIsMissingOrOutOfRange(int? requested, int expected)
    {
        // Arrange
        // Act
        var result = TripConstants.NormalisePlannedWindowDays(requested);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Planned", true)]
    [InlineData("Cancelled", true)]
    [InlineData("Active", false)]
    [InlineData("Completed", false)]
    [InlineData("planned", false)]
    [InlineData(null, false)]
    public void ItShouldRecogniseThePlannedLifecycleStatuses(string? status, bool expected)
    {
        // Arrange
        // Act
        var result = TripConstants.HasPlannedLifecycle(status);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Active", true)]
    [InlineData("Completed", true)]
    [InlineData("Planned", true)]
    [InlineData("Cancelled", true)]
    [InlineData("Abandoned", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ItShouldKnowEveryLifecycleStatus(string? status, bool expected)
    {
        // Arrange
        // Act
        var result = TripConstants.IsKnownStatus(status);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ItShouldRequireANonDefaultPlannedStart()
    {
        // Arrange
        // Act
        var missing = TripConstants.IsPlannedStartOnValid(null);
        var unset = TripConstants.IsPlannedStartOnValid(default(DateTimeOffset));
        var set = TripConstants.IsPlannedStartOnValid(DateTimeOffset.Parse("2026-09-12T06:00:00Z"));

        // Assert
        missing.Should().BeFalse();
        unset.Should().BeFalse();
        set.Should().BeTrue();
    }
}
