using AwesomeAssertions;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Mobile.Tests.Features.Dashboard.ViewModels.DashboardFilterViewModelTests;

public class WhenTestingApply : BaseDashboardFilterViewModelTest
{
    [Theory]
    [InlineData("2026-03-02", "2026-03-01")]
    [InlineData("9999-12-31", "9999-12-31")]
    public void ItShouldKeepInvalidCustomDatesInTheModalWithoutReturningAnAppliedSelection(string from, string to)
    {
        // Arrange
        Sut.SelectPreset((int)DashboardDatePresetEnum.Custom);
        Sut.FromDate = DateTime.Parse(from, System.Globalization.CultureInfo.InvariantCulture);
        Sut.ToDate = DateTime.Parse(to, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        var result = Sut.Apply();

        // Assert
        result.Should().BeNull();
        Sut.HasValidationError.Should().BeTrue();
        Sut.ValidationText.Should().Contain("date");
        Applied.Preset.Should().Be(DashboardDatePresetEnum.ThisYear);
    }

    [Fact]
    public void ItShouldReturnAnImmutableSnapshotWithAllFiltersAndInclusiveCustomDates()
    {
        // Arrange
        Sut.SelectPreset((int)DashboardDatePresetEnum.Custom);
        Sut.FromDate = Sut.ToDate = new DateTime(2026, 3, 1);
        Sut.SelectedSpecies = Sut.SpeciesOptions.Single(choice => choice.Value == "Pike");
        Sut.SelectedMethod = Sut.MethodOptions.Single(choice => choice.Value == "Fly");
        Sut.SelectedVenue = Sut.VenueOptions.Single(choice => choice.Value == "Lake");

        // Act
        var result = Sut.Apply();
        Sut.FromDate = new DateTime(2026, 4, 1);
        Sut.SelectedSpecies = null;

        // Assert
        result!.Preset.Should().Be(DashboardDatePresetEnum.Custom);
        result.From.Should().Be(new DateTime(2026, 3, 1));
        result.To.Should().Be(result.From);
        result.Species.Should().Be("Pike");
        result.Method.Should().Be("Fly");
        result.Venue.Should().Be("Lake");
        Sut.HasValidationError.Should().BeFalse();
        Applied.Species.Should().BeNull();
    }
}
