using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Catch.ViewModels;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
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

    [Fact]
    public async Task ItShouldApplyTheDefaultSpeciesWhenTheMethodChangesAndSpeciesIsNotExplicit()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());
        await Sut.InitializeAsync(null, CancellationToken.None);
        Sut.IsSpeciesSelected("Salmon").Should().BeTrue();

        // Act
        Sut.SelectMethod("Spinning");

        // Assert
        Sut.IsMethodSelected("Spinning").Should().BeTrue();
        Sut.IsSpeciesSelected("Salmon").Should().BeFalse();
        Sut.SpeciesOptions.Should().NotContain(option => option.IsSelected);
    }

    [Fact]
    public async Task ItShouldKeepAnExplicitSpeciesWhenTheMethodChanges()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());
        await Sut.InitializeAsync(null, CancellationToken.None);
        Sut.ChooseSpecies(CatchChipOptionModel.FromValue("Brown Trout"));

        // Act
        Sut.SelectMethod("Spinning");

        // Assert
        Sut.IsSpeciesSelected("Brown Trout").Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldIgnoreSelectingTheSameMethodAgain()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());
        await Sut.InitializeAsync(null, CancellationToken.None);

        // Act
        Sut.SelectMethod("Fly");

        // Assert
        Sut.IsMethodSelected("Fly").Should().BeTrue();
        Sut.IsSpeciesSelected("Salmon").Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldClearTheDefaultSpeciesWhenTheMethodNameIsEmpty()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());
        await Sut.InitializeAsync(null, CancellationToken.None);

        // Act
        Sut.SelectMethod(string.Empty);

        // Assert
        Sut.IsMethodSelected("Fly").Should().BeFalse();
        Sut.IsSpeciesSelected("Salmon").Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldSelectAMethodByPreferenceCode()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(
            new AnglerPreferencesModel(
                TestAnglerPreferences.Catalogue(),
                new FishingPreferencesDto(
                [
                    new FishingMethodPreferenceDto(
                        TestAnglerPreferences.FlyId,
                        "fly-code",
                        "Fly",
                        true,
                        [
                            new FishingSpeciesPreferenceDto(
                                TestAnglerPreferences.SalmonId,
                                "Salmon",
                                "Salmon",
                                true)
                        ])
                ]),
                TestAnglerPreferences.Locations()));
        await Sut.InitializeAsync(null, CancellationToken.None);

        // Act
        Sut.SelectMethod("fly-code");

        // Assert
        Sut.IsMethodSelected("fly-code").Should().BeTrue();
        Sut.IsSpeciesSelected("Salmon").Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldIgnoreSpeciesSelectionAfterSave()
    {
        // Arrange
        MockCatchStore.SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>())
            .Returns(call => LocalCatchSaveResultModel.Saved(call.Arg<LocalCatchSaveArgs>().Id, 1));
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());
        await Sut.InitializeAsync(null, CancellationToken.None);
        Sut.Photographs.SetItems(
        [
            new PhotographEditorItemModel
            {
                Id = Guid.NewGuid(),
                SortOrder = 0,
                PreviewPath = "a.jpg",
                RelativeFilePath = "a.jpg",
                ContentType = "image/jpeg"
            }
        ]);
        (await Sut.SaveAsync(CancellationToken.None)).Should().BeTrue();

        // Act
        Sut.SelectSpecies("Brown Trout");
        Sut.SelectMethod("Spinning");

        // Assert
        Sut.IsSpeciesSelected("Salmon").Should().BeTrue();
        Sut.IsMethodSelected("Fly").Should().BeTrue();
    }
}
