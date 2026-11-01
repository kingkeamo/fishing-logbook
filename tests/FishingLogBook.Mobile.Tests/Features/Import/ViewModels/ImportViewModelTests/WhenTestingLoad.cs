using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Mobile.Features.Import.ViewModels;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Import.ViewModels.ImportViewModelTests;

public sealed class WhenTestingLoad : BaseImportViewModelTest
{
    [Fact]
    public async Task ItShouldApplyPreferredDefaultsAndKeepTheWizardOnBatchDetails()
    {
        // Arrange
        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        Sut.IsBatchStep.Should().BeTrue();
        Sut.StepNumber.Should().Be(1);
        Sut.CanGoBack.Should().BeFalse();
        Sut.CanContinue.Should().BeTrue();
        Sut.MethodOptions.Should().Contain(option => option.Id == MethodId && option.IsSelected);
        Sut.MethodOptions.Should().Contain(option => option.Code == ImportViewModel.MoreOptionCode);
        Sut.SpeciesOptions.Should().Contain(option => option.Id == SpeciesId && option.IsSelected);
        await MockPreferences.Received(1).RefreshAsync(Arg.Any<CancellationToken>());
        await MockUnits.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }
}
