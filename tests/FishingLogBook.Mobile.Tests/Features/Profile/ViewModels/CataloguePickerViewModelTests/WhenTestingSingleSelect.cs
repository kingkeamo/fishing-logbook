using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Profile.ViewModels;

namespace FishingLogBook.Mobile.Tests.Features.Profile.ViewModels.CataloguePickerViewModelTests;

public sealed class WhenTestingSingleSelect : BaseCataloguePickerViewModelTest
{
    [Fact]
    public void ItShouldClearOtherSelectionsWhenAllowMultipleIsFalse()
    {
        // Arrange
        var fly = Option("Fly", isSelected: true);
        var spinning = Option("Spinning");
        var sut = new CataloguePickerViewModel(Text, "Method", [fly, spinning], allowMultiple: false);

        // Act
        sut.Toggle(spinning);

        // Assert
        fly.IsSelected.Should().BeFalse();
        spinning.IsSelected.Should().BeTrue();
        sut.SelectedOptions.Should().ContainSingle().Which.Should().BeSameAs(spinning);
    }

    [Fact]
    public void ItShouldListAllOptionsWithoutInitialLimitWhenAllowMultipleIsFalse()
    {
        // Arrange
        var options = Options(CataloguePickerViewModel.InitialOptionLimit + 5);

        // Act
        var sut = new CataloguePickerViewModel(Text, "Species", options, allowMultiple: false);

        // Assert
        sut.Options.Should().HaveCount(options.Length);
        sut.IsLimited.Should().BeFalse();
    }
}
