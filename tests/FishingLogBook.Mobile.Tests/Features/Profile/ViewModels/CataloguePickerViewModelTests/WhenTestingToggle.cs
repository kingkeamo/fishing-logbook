using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Profile.ViewModels;

namespace FishingLogBook.Mobile.Tests.Features.Profile.ViewModels.CataloguePickerViewModelTests;

public sealed class WhenTestingToggle : BaseCataloguePickerViewModelTest
{
    [Fact]
    public void ItShouldAddAnUnselectedOptionToTheSelection()
    {
        // Arrange
        var salmon = Option("Salmon");
        var sut = CreateSut(salmon);
        var changes = new List<string?>();
        sut.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        // Act
        sut.Toggle(salmon);

        // Assert
        salmon.IsSelected.Should().BeTrue();
        sut.SelectedOptions.Should().ContainSingle().Which.Should().BeSameAs(salmon);
        sut.HasNoSelection.Should().BeFalse();
        changes.Should().Contain([nameof(CataloguePickerViewModel.SelectedOptions), nameof(CataloguePickerViewModel.HasNoSelection)]);
    }

    [Fact]
    public void ItShouldRemoveASelectedOptionFromTheSelection()
    {
        // Arrange
        var salmon = Option("Salmon", isSelected: true);
        var sut = CreateSut(salmon);

        // Act
        sut.Toggle(salmon);

        // Assert
        salmon.IsSelected.Should().BeFalse();
        sut.SelectedOptions.Should().BeEmpty();
        sut.HasNoSelection.Should().BeTrue();
    }

    [Fact]
    public void ItShouldShowSelectedOptionsOutsideTheInitialLimit()
    {
        // Arrange
        var options = Options(CataloguePickerViewModel.InitialOptionLimit + 1);
        var last = options[^1];
        var sut = CreateSut(options);
        sut.Search = last.Name;
        sut.Toggle(last);

        // Act
        sut.Search = string.Empty;

        // Assert
        sut.Options.Should().NotContain(last);
        sut.SelectedOptions.Should().ContainSingle().Which.Should().BeSameAs(last);
    }
}
