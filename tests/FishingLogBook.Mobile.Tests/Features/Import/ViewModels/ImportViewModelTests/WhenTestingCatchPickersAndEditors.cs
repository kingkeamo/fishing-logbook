using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Import.ViewModels;
using FishingLogBook.Mobile.Features.Measurements.Models;
using FishingLogBook.Mobile.Features.Profile.Models;

namespace FishingLogBook.Mobile.Tests.Features.Import.ViewModels.ImportViewModelTests;

public sealed class WhenTestingCatchPickersAndEditors : BaseImportCatchActionsTest
{
    [Fact]
    public async Task ItShouldListTheCatalogueMethodsWithTheCatchesMethodSelected()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());
        var catchId = Sut.Catches.Single().Proposal.Id;

        // Act
        var picker = Sut.CreateCatchMethodPicker(catchId);

        // Assert
        picker.AllowMultiple.Should().BeFalse();
        picker.Options.Select(option => option.Name).Should().BeEquivalentTo("Fly", "Spinning");
        picker.SelectedOptions.Should().ContainSingle().Which.Id.Should().Be(MethodId);
    }

    [Fact]
    public async Task ItShouldListTheCatalogueSpeciesWithTheCatchesSpeciesSelected()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());
        var catchId = Sut.Catches.Single().Proposal.Id;

        // Act
        var picker = Sut.CreateCatchSpeciesPicker(catchId);

        // Assert
        picker.Options.Should().ContainSingle().Which.Name.Should().Be("Brown trout");
        picker.SelectedOptions.Should().ContainSingle().Which.Id.Should().Be(SpeciesId);
    }

    [Fact]
    public async Task ItShouldRefuseAChipThatIsNotBoundToACatch()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());

        // Act
        var method = Sut.ChooseCatchMethod(new ImportOptionModel(OtherMethodId, "SPIN", "Spinning"));
        var species = Sut.ChooseCatchSpecies(null);

        // Assert
        method.Should().BeFalse();
        species.Should().BeFalse();
        Sut.Catches.Single().MethodName.Should().Be("Fly");
    }

    [Fact]
    public async Task ItShouldAskThePageToOpenTheCataloguesWhenMoreIsChosenForACatch()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());
        var row = Sut.Catches.Single();
        var moreMethod = row.MethodOptions.Single(option => option.Code == ImportViewModel.MoreOptionCode);
        var moreSpecies = row.SpeciesOptions.Single(option => option.Code == ImportViewModel.MoreOptionCode);

        // Act
        var openMethods = Sut.ChooseCatchMethod(moreMethod);
        var openSpecies = Sut.ChooseCatchSpecies(moreSpecies);

        // Assert
        openMethods.Should().BeTrue();
        openSpecies.Should().BeTrue();
        Sut.Catches.Single().MethodName.Should().Be("Fly");
    }

    [Fact]
    public async Task ItShouldOverrideOnlyThatCatchesMethodWhenAChipIsChosen()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());
        var catchId = Sut.Catches.Single().Proposal.Id;
        var spinning = new ImportOptionModel(OtherMethodId, "SPIN", "Spinning", CatchId: catchId);

        // Act
        var openPicker = Sut.ChooseCatchMethod(spinning);

        // Assert
        openPicker.Should().BeFalse();
        Sut.Catches.Single().MethodName.Should().Be("Spinning");
        Sut.MethodOptions.Single(option => option.IsSelected).Id.Should().Be(MethodId);
    }

    [Fact]
    public async Task ItShouldKeepTheCatchSpeciesWhenTheSameSpeciesChipIsChosen()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());
        var row = Sut.Catches.Single();
        var species = row.SpeciesOptions.Single(option => option.Id == SpeciesId);

        // Act
        var openPicker = Sut.ChooseCatchSpecies(species);

        // Assert
        openPicker.Should().BeFalse();
        Sut.Catches.Single().SpeciesName.Should().Be("Brown trout");
    }

    [Fact]
    public async Task ItShouldKeepTheCatchMethodWhenTheCatchMethodPickerIsDismissedOrUnknown()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());
        var catchId = Sut.Catches.Single().Proposal.Id;

        // Act
        Sut.CompleteCatchMethodPicker(catchId, null);
        Sut.CompleteCatchMethodPicker(catchId, new CataloguePickerResultModel(new HashSet<Guid> { Guid.NewGuid() }));

        // Assert
        Sut.Catches.Single().MethodName.Should().Be("Fly");
    }

    [Fact]
    public async Task ItShouldKeepTheCatchSpeciesWhenTheCatchSpeciesPickerIsDismissedOrUnknown()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());
        var catchId = Sut.Catches.Single().Proposal.Id;

        // Act
        Sut.CompleteCatchSpeciesPicker(catchId, null);
        Sut.CompleteCatchSpeciesPicker(catchId, new CataloguePickerResultModel(new HashSet<Guid> { Guid.NewGuid() }));

        // Assert
        Sut.Catches.Single().SpeciesName.Should().Be("Brown trout");
    }

    [Fact]
    public async Task ItShouldApplyACatchMethodPickerChoice()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());
        var catchId = Sut.Catches.Single().Proposal.Id;

        // Act
        Sut.CompleteCatchMethodPicker(catchId, new CataloguePickerResultModel(new HashSet<Guid> { OtherMethodId }));

        // Assert
        Sut.Catches.Single().MethodName.Should().Be("Spinning");
    }

    [Fact]
    public async Task ItShouldApplyACatchSpeciesPickerChoice()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());
        var catchId = Sut.Catches.Single().Proposal.Id;

        // Act
        Sut.CompleteCatchSpeciesPicker(catchId, new CataloguePickerResultModel(new HashSet<Guid> { SpeciesId }));

        // Assert
        Sut.Catches.Single().SpeciesName.Should().Be("Brown trout");
    }

    [Fact]
    public async Task ItShouldIgnoreAnUnknownCatchMethodOrSpecies()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());
        var catchId = Sut.Catches.Single().Proposal.Id;

        // Act
        Sut.SetCatchMethod(catchId, Guid.NewGuid());
        Sut.SetCatchSpecies(catchId, Guid.NewGuid());

        // Assert
        Sut.Catches.Single().MethodName.Should().Be("Fly");
        Sut.Catches.Single().SpeciesName.Should().Be("Brown trout");
    }

    [Fact]
    public async Task ItShouldOpenAWeightEditorForTheCatchesCurrentWeight()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());
        var catchId = Sut.Catches.Single().Proposal.Id;

        // Act
        var editor = Sut.CreateWeightEditor(catchId);

        // Assert
        editor.Kind.Should().Be(MeasurementKindEnum.Weight);
        editor.CanonicalValue.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldOpenALengthEditorForTheCatchesCurrentLength()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());
        var catchId = Sut.Catches.Single().Proposal.Id;

        // Act
        var editor = Sut.CreateLengthEditor(catchId);

        // Assert
        editor.Kind.Should().Be(MeasurementKindEnum.Length);
        editor.CanonicalValue.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldNotChangeTheCatchWhenAMeasurementEditIsCancelled()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());
        var catchId = Sut.Catches.Single().Proposal.Id;
        var weight = Sut.CreateWeightEditor(catchId);
        weight.SetSliderValue(3m);
        var length = Sut.CreateLengthEditor(catchId);
        length.SetSliderValue(40m);

        // Act
        Sut.CompleteWeightEdit(catchId, false, weight);
        Sut.CompleteLengthEdit(catchId, false, length);

        // Assert
        Sut.Catches.Single().Proposal.Weight.Should().BeNull();
        Sut.Catches.Single().Proposal.Length.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldStoreTheCanonicalWeightAndLengthWhenMeasurementEditsAreApplied()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());
        var catchId = Sut.Catches.Single().Proposal.Id;
        var weight = Sut.CreateWeightEditor(catchId);
        weight.SetExactText("3");
        var length = Sut.CreateLengthEditor(catchId);
        length.SetExactText("40");

        // Act
        Sut.CompleteWeightEdit(catchId, true, weight);
        Sut.CompleteLengthEdit(catchId, true, length);

        // Assert
        var proposal = Sut.Catches.Single().Proposal;
        proposal.Weight.Should().Be(weight.CanonicalValue).And.NotBeNull();
        proposal.Length.Should().Be(length.CanonicalValue).And.NotBeNull();
        Sut.Catches.Single().WeightText.Should().NotBe(Text["MeasureNotRecorded"]);
    }
}
