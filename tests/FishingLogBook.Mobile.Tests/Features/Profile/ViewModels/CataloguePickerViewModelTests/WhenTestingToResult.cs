using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Features.Profile.ViewModels.CataloguePickerViewModelTests;

public sealed class WhenTestingToResult : BaseCataloguePickerViewModelTest
{
    [Fact]
    public void ItShouldReturnTheCompleteSelectionIncludingOptionsHiddenBySearch()
    {
        // Arrange
        var salmon = Option("Salmon", isSelected: true);
        var trout = Option("Brown trout");
        var pike = Option("Pike", isSelected: true);
        var sut = CreateSut(salmon, trout, pike);
        sut.Search = "trout";
        sut.Toggle(trout);

        // Act
        var result = sut.ToResult();

        // Assert
        result.SelectedIds.Should().BeEquivalentTo([salmon.Id, trout.Id, pike.Id]);
    }

    [Fact]
    public void ItShouldReturnAnEmptySelectionWhenEverythingIsRemoved()
    {
        // Arrange
        var salmon = Option("Salmon", isSelected: true);
        var sut = CreateSut(salmon);
        sut.Toggle(salmon);

        // Act
        var result = sut.ToResult();

        // Assert
        result.SelectedIds.Should().BeEmpty();
    }
}
