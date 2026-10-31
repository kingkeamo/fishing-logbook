using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Catch.ViewModels;
using FishingLogBook.Mobile.Tests.Support;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchEditViewModelTests;

public sealed class WhenTestingChipAndEditorCompletion : BaseCatchEditViewModelTest
{
    [Fact]
    public async Task ItShouldSelectAMethodChipWithoutOpeningThePicker()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());
        await Sut.InitializeAsync(null, CancellationToken.None);

        // Act
        var needsPicker = Sut.ChooseMethod(CatchChipOptionModel.FromValue("Spinning"));

        // Assert
        needsPicker.Should().BeFalse();
        Sut.MethodOptions.Should().Contain(option => option.Name == "Spinning" && option.IsSelected);
        Sut.MethodOptions.Should().NotContain(option => option.Name == "Fly" && option.IsSelected);
    }

    [Fact]
    public async Task ItShouldRequestTheCataloguePickerForMoreWithoutChangingTheMethod()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());
        await Sut.InitializeAsync(null, CancellationToken.None);
        var current = Sut.MethodOptions.Single(option => option.IsSelected).Name;

        // Act
        var needsPicker = Sut.ChooseMethod(new CatchChipOptionModel(CatchEditViewModel.MoreOptionCode, "More"));

        // Assert
        needsPicker.Should().BeTrue();
        Sut.MethodOptions.Should().Contain(option => option.Name == current && option.IsSelected);
    }

    [Fact]
    public async Task ItShouldIgnoreACancelledMethodPicker()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());
        await Sut.InitializeAsync(null, CancellationToken.None);
        var current = Sut.MethodOptions.Single(option => option.IsSelected).Name;

        // Act
        Sut.CompleteMethodPicker(null);

        // Assert
        Sut.MethodOptions.Should().Contain(option => option.Name == current && option.IsSelected);
    }

    [Fact]
    public async Task ItShouldApplyACompletedMethodPickerSelection()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());
        await Sut.InitializeAsync(null, CancellationToken.None);
        var picker = Sut.CreateMethodPicker();
        picker.Toggle(picker.Options.Single(option => option.Id == TestAnglerPreferences.SpinningId));

        // Act
        Sut.CompleteMethodPicker(picker.ToResult());

        // Assert
        Sut.MethodOptions.Should().Contain(option => option.Name == "Spinning" && option.IsSelected);
    }

    [Fact]
    public async Task ItShouldIgnoreACancelledSpeciesPicker()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());
        await Sut.InitializeAsync(null, CancellationToken.None);
        var current = Sut.SpeciesOptions.Single(option => option.IsSelected).Name;

        // Act
        Sut.CompleteSpeciesPicker(null);

        // Assert
        Sut.SpeciesOptions.Should().Contain(option => option.Name == current && option.IsSelected);
    }

    [Fact]
    public async Task ItShouldApplyACompletedSpeciesPickerSelection()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());
        await Sut.InitializeAsync(null, CancellationToken.None);
        var picker = Sut.CreateSpeciesPicker();
        picker.Toggle(picker.Options.Single(option => option.Id == TestAnglerPreferences.BrownTroutId));

        // Act
        Sut.CompleteSpeciesPicker(picker.ToResult());

        // Assert
        Sut.SpeciesOptions.Should().Contain(option => option.Name == "Brown Trout" && option.IsSelected);
    }

    [Fact]
    public async Task ItShouldKeepCatchWeightWhenMeasurementEditIsCancelled()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        var applied = Sut.CreateWeightEditor();
        applied.SetExactText("2.2");
        Sut.CompleteWeightEdit(true, applied);
        var cancelled = Sut.CreateWeightEditor();
        cancelled.SetExactText("9.9");

        // Act
        Sut.CompleteWeightEdit(false, cancelled);

        // Assert
        Sut.WeightSummary.Should().Contain("2.2");
        Sut.WeightSummary.Should().NotContain("9.9");
    }

    [Fact]
    public async Task ItShouldKeepCatchLengthWhenMeasurementEditIsCancelled()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        var applied = Sut.CreateLengthEditor();
        applied.SetExactText("45");
        Sut.CompleteLengthEdit(true, applied);
        var cancelled = Sut.CreateLengthEditor();
        cancelled.SetExactText("12");

        // Act
        Sut.CompleteLengthEdit(false, cancelled);

        // Assert
        Sut.LengthSummary.Should().Contain("45");
        Sut.LengthSummary.Should().NotContain("12");
    }

    [Fact]
    public void ItShouldIgnoreANullMethodChip()
    {
        // Act
        var needsPicker = Sut.ChooseMethod(null);

        // Assert
        needsPicker.Should().BeFalse();
    }
}
