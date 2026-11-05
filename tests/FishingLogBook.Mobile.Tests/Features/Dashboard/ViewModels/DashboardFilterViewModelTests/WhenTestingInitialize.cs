using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Dashboard.Models;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Mobile.Tests.Features.Dashboard.ViewModels.DashboardFilterViewModelTests;

public class WhenTestingInitialize : BaseDashboardFilterViewModelTest
{
    [Theory]
    [InlineData("en-GB", "This year", "All")]
    [InlineData("fr-FR", "Cette année", "Tous")]
    public void ItShouldLocalizeChoicesAndInitializeTheAppliedValues(string culture, string dateLabel, string allLabel)
    {
        // Arrange
        Text.SetCulture(culture);
        var selection = Applied with { Preset = DashboardDatePresetEnum.Custom, Species = "Pike", Method = "Fly", Venue = "Lake" };

        // Act
        Sut.Initialize(selection, Options);

        // Assert
        Sut.DatePresets.Should().Contain(dateLabel);
        Sut.SpeciesOptions[0].Label.Should().Be(allLabel);
        Sut.SelectedSpecies!.Value.Should().Be("Pike");
        Sut.SelectedMethod!.Value.Should().Be("Fly");
        Sut.SelectedVenue!.Value.Should().Be("Lake");
        Sut.FromDate.Should().Be(selection.From);
        Sut.ToDate.Should().Be(selection.To);
        Sut.IsCustom.Should().BeTrue();
    }

    [Fact]
    public void ItShouldRetainAppliedChoicesThatAreNoLongerInTheAvailableOptions()
    {
        // Arrange
        var selection = Applied with { Species = "Salmon", Method = "Other", Venue = "River" };

        // Act
        Sut.Initialize(selection, Options);

        // Assert
        Sut.SelectedSpecies!.Value.Should().Be("Salmon");
        Sut.SelectedMethod!.Value.Should().Be("Other");
        Sut.SelectedVenue!.Value.Should().Be("River");
        Sut.Apply().Should().BeEquivalentTo(selection);
    }
}
