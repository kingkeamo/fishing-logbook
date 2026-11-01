using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Mobile.Features.Import.ViewModels;

namespace FishingLogBook.Mobile.Tests.Features.Import.ViewModels.ImportViewModelTests;

public sealed class WhenTestingContinue : BaseImportViewModelTest
{
    [Fact]
    public async Task ItShouldMoveFromBatchDetailsToChoosePhotos()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.IsPhotosStep.Should().BeTrue();
        Sut.StepNumber.Should().Be(2);
        Sut.CanGoBack.Should().BeTrue();
        Sut.CanContinue.Should().BeFalse();
        Sut.Stage.Should().Be(ImportStageEnum.ChoosePhotos);
    }

    [Fact]
    public async Task ItShouldReturnToBatchDetailsFromChoosePhotos()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        await Sut.ContinueAsync(CancellationToken.None);

        // Act
        Sut.Back();

        // Assert
        Sut.IsBatchStep.Should().BeTrue();
        Sut.CanGoBack.Should().BeFalse();
    }
}
