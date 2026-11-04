using AwesomeAssertions;
using FishingLogBook.Shared.Dashboard.Models;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Shared.Tests.Dashboard.DashboardDateRangeModelTests;

public class WhenTestingResolve
{
    [Theory]
    [InlineData(DashboardDatePresetEnum.ThisMonth, "2024-02-01", "2024-02-29")]
    [InlineData(DashboardDatePresetEnum.ThisYear, "2024-01-01", "2024-12-31")]
    [InlineData(DashboardDatePresetEnum.LastYear, "2023-01-01", "2023-12-31")]
    [InlineData(DashboardDatePresetEnum.Custom, "2023-12-31", "2024-01-01")]
    public void ItShouldResolveInclusiveCalendarDates(DashboardDatePresetEnum preset, string from, string to)
    {
        // Arrange
        var today = new DateOnly(2024, 2, 15);

        // Act
        var range = DashboardDateRangeModel.Resolve(preset, today, new DateOnly(2023, 12, 31), new DateOnly(2024, 1, 1));

        // Assert
        range.From.Should().Be(DateOnly.Parse(from));
        range.To.Should().Be(DateOnly.Parse(to));
    }

    [Fact]
    public void ItShouldLeaveAllTimeUnbounded()
    {
        // Arrange
        var today = new DateOnly(2026, 1, 1);

        // Act
        var range = DashboardDateRangeModel.Resolve(DashboardDatePresetEnum.AllTime, today, default, default);

        // Assert
        range.From.Should().BeNull();
        range.To.Should().BeNull();
    }

    [Fact]
    public void ItShouldRejectUnsupportedPresets()
    {
        // Arrange
        var action = () => DashboardDateRangeModel.Resolve((DashboardDatePresetEnum)99, new DateOnly(2026, 1, 1), default, default);

        // Act
        var result = action.Should();

        // Assert
        result.Throw<ArgumentOutOfRangeException>();
    }
}
