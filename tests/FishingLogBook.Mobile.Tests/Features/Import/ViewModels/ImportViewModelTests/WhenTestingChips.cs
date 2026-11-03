using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Import.ViewModels;
using FishingLogBook.Mobile.Features.Profile.Models;

namespace FishingLogBook.Mobile.Tests.Features.Import.ViewModels.ImportViewModelTests;

public sealed class WhenTestingChips : BaseImportViewModelTest
{
    [Fact]
    public async Task ItShouldAskThePageToOpenTheCatalogueWhenMoreIsChosen()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        var more = Sut.MethodOptions.Single(option => option.Code == ImportViewModel.MoreOptionCode);

        // Act
        var openPicker = Sut.ChooseMethod(more);

        // Assert
        openPicker.Should().BeTrue();
        Sut.MethodOptions.Single(option => option.Id == MethodId).IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldSelectACatalogueMethodWithoutOpeningThePicker()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        var spinning = new ImportOptionModel(OtherMethodId, "SPIN", "Spinning");

        // Act
        var openPicker = Sut.ChooseMethod(spinning);

        // Assert
        openPicker.Should().BeFalse();
        Sut.MethodOptions.Single(option => option.Id == OtherMethodId).IsSelected.Should().BeTrue();
        Sut.MethodOptions.Single(option => option.Id == MethodId).IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldApplyASingleCataloguePickerChoice()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        Sut.CompleteMethodPicker(new CataloguePickerResultModel(new HashSet<Guid> { OtherMethodId }));

        // Assert
        Sut.MethodOptions.Single(option => option.Id == OtherMethodId).IsSelected.Should().BeTrue();
    }
}
