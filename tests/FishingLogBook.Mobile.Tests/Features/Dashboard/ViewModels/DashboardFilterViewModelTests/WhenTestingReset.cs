using AwesomeAssertions;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Mobile.Tests.Features.Dashboard.ViewModels.DashboardFilterViewModelTests;

public class WhenTestingReset : BaseDashboardFilterViewModelTest
{
    [Fact]
    public void ItShouldReturnThisYearAndAllClassificationsForTheDashboardToApply()
    {
        // Arrange
        Sut.Initialize(Applied with { Preset = DashboardDatePresetEnum.AllTime, Species = "Pike", Method = "Fly", Venue = "Lake" }, Options);

        // Act
        var result = Sut.Reset();

        // Assert
        result.Preset.Should().Be(DashboardDatePresetEnum.ThisYear);
        result.From.Should().Be(new DateTime(2026, 1, 1));
        result.To.Should().Be(new DateTime(2026, 12, 31));
        result.Species.Should().BeNull();
        result.Method.Should().BeNull();
        result.Venue.Should().BeNull();
    }
}
