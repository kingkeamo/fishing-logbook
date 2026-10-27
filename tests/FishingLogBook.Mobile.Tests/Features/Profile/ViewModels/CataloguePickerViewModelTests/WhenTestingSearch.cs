using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Profile.ViewModels;

namespace FishingLogBook.Mobile.Tests.Features.Profile.ViewModels.CataloguePickerViewModelTests;

public sealed class WhenTestingSearch : BaseCataloguePickerViewModelTest
{
    [Fact]
    public void ItShouldLimitTheInitialAvailableListAndExplainHowToFindMore()
    {
        // Arrange
        var options = Options(CataloguePickerViewModel.InitialOptionLimit + 5);

        // Act
        var sut = CreateSut(options);

        // Assert
        sut.Options.Should().Equal(options.Take(CataloguePickerViewModel.InitialOptionLimit));
        sut.IsLimited.Should().BeTrue();
        sut.LimitMessage.Should().Be("Showing 20 of 25. Search to find more.");
    }

    [Fact]
    public void ItShouldShowEveryOptionWhenTheCatalogueIsWithinTheLimit()
    {
        // Arrange
        var options = Options(CataloguePickerViewModel.InitialOptionLimit);

        // Act
        var sut = CreateSut(options);

        // Assert
        sut.Options.Should().Equal(options);
        sut.IsLimited.Should().BeFalse();
    }

    [Fact]
    public void ItShouldSearchTheWholeCatalogueIgnoringCaseAndSurroundingSpaces()
    {
        // Arrange
        var options = Options(CataloguePickerViewModel.InitialOptionLimit + 5);
        var sut = CreateSut(options);

        // Act
        sut.Search = "  SPECIES 2  ";

        // Assert
        sut.Options.Select(option => option.Name).Should().Equal(
            "Species 20", "Species 21", "Species 22", "Species 23", "Species 24", "Species 25");
        sut.IsLimited.Should().BeFalse();
        sut.HasNoMatches.Should().BeFalse();
    }

    [Fact]
    public void ItShouldReportWhenNothingMatches()
    {
        // Arrange
        var sut = CreateSut(Option("Salmon"), Option("Brown trout"));

        // Act
        sut.Search = "pike";

        // Assert
        sut.Options.Should().BeEmpty();
        sut.HasNoMatches.Should().BeTrue();
    }

    [Fact]
    public void ItShouldKeepTheDraftSelectionWhileFiltering()
    {
        // Arrange
        var salmon = Option("Salmon", isSelected: true);
        var trout = Option("Brown trout");
        var sut = CreateSut(salmon, trout);

        // Act
        sut.Search = "trout";

        // Assert
        sut.Options.Should().ContainSingle().Which.Should().BeSameAs(trout);
        sut.SelectedOptions.Should().ContainSingle().Which.Should().BeSameAs(salmon);
    }
}
