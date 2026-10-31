using AwesomeAssertions;
using FishingLogBook.Mobile.Tests.Support;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchEditViewModelTests;

public sealed class WhenTestingCataloguePicker : BaseCatchEditViewModelTest
{
    [Fact]
    public async Task ItShouldApplyMethodChosenFromCataloguePicker()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());
        await Sut.InitializeAsync(null, CancellationToken.None);
        var picker = Sut.CreateMethodPicker();
        picker.Toggle(picker.Options.Single(option => option.Id == TestAnglerPreferences.SpinningId));

        // Act
        Sut.ApplyMethodPicker(picker.ToResult());

        // Assert
        Sut.MethodOptions.Should().Contain(option => option.Name == "Spinning" && option.IsSelected);
        Sut.MethodOptions.Should().NotContain(option => option.Name == "Fly" && option.IsSelected);
    }

    [Fact]
    public async Task ItShouldApplySpeciesChosenFromCataloguePicker()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());
        await Sut.InitializeAsync(null, CancellationToken.None);
        var picker = Sut.CreateSpeciesPicker();
        picker.Toggle(picker.Options.Single(option => option.Id == TestAnglerPreferences.BrownTroutId));

        // Act
        Sut.ApplySpeciesPicker(picker.ToResult());

        // Assert
        Sut.SpeciesOptions.Should().Contain(option => option.Name == "Brown Trout" && option.IsSelected);
    }

    [Fact]
    public async Task ItShouldCreateSingleSelectMethodPickerPreselectingCurrentMethod()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());
        await Sut.InitializeAsync(null, CancellationToken.None);

        // Act
        var picker = Sut.CreateMethodPicker();

        // Assert
        picker.AllowMultiple.Should().BeFalse();
        picker.SelectedOptions.Should().ContainSingle(option => option.Id == TestAnglerPreferences.FlyId);
    }
}
