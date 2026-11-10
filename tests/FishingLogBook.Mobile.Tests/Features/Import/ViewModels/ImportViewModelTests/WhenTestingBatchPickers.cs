using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Import.ViewModels;
using FishingLogBook.Mobile.Features.Profile.Models;

namespace FishingLogBook.Mobile.Tests.Features.Import.ViewModels.ImportViewModelTests;

public sealed class WhenTestingBatchPickers : BaseImportViewModelTest
{
    [Fact]
    public async Task ItShouldListEveryCatalogueMethodWithTheCurrentOneSelected()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        var picker = Sut.CreateMethodPicker();

        // Assert
        picker.AllowMultiple.Should().BeFalse();
        picker.Title.Should().Be(Text["Import_FishingMethod"]);
        picker.Options.Select(option => option.Name).Should().BeEquivalentTo("Fly", "Spinning");
        picker.SelectedOptions.Should().ContainSingle().Which.Id.Should().Be(MethodId);
    }

    [Fact]
    public async Task ItShouldListEveryCatalogueSpeciesWithTheCurrentOneSelected()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        var picker = Sut.CreateSpeciesPicker();

        // Assert
        picker.AllowMultiple.Should().BeFalse();
        picker.Title.Should().Be(Text["Import_Species"]);
        picker.Options.Should().ContainSingle().Which.Name.Should().Be("Brown trout");
        picker.SelectedOptions.Should().ContainSingle().Which.Id.Should().Be(SpeciesId);
    }

    [Fact]
    public async Task ItShouldIgnoreAMethodPickerResultThatIsNotInTheCatalogue()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        Sut.ApplyMethodPicker(new CataloguePickerResultModel(new HashSet<Guid> { Guid.NewGuid() }));

        // Assert
        Sut.MethodOptions.Single(option => option.IsSelected).Id.Should().Be(MethodId);
    }

    [Fact]
    public async Task ItShouldIgnoreASpeciesPickerResultThatIsNotInTheCatalogue()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        Sut.ApplySpeciesPicker(new CataloguePickerResultModel(new HashSet<Guid> { Guid.NewGuid() }));

        // Assert
        Sut.SpeciesOptions.Single(option => option.IsSelected).Id.Should().Be(SpeciesId);
    }

    [Fact]
    public async Task ItShouldKeepTheCurrentMethodWhenTheMethodPickerIsDismissed()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        Sut.CompleteMethodPicker(null);

        // Assert
        Sut.MethodOptions.Single(option => option.IsSelected).Id.Should().Be(MethodId);
    }

    [Fact]
    public async Task ItShouldKeepTheCurrentSpeciesWhenTheSpeciesPickerIsDismissed()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        Sut.CompleteSpeciesPicker(null);

        // Assert
        Sut.SpeciesOptions.Single(option => option.IsSelected).Id.Should().Be(SpeciesId);
    }

    [Fact]
    public async Task ItShouldApplyASpeciesPickerChoice()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        Sut.CompleteSpeciesPicker(new CataloguePickerResultModel(new HashSet<Guid> { SpeciesId }));

        // Assert
        Sut.SpeciesOptions.Single(option => option.IsSelected).Id.Should().Be(SpeciesId);
    }

    [Fact]
    public async Task ItShouldRefuseToChooseANullChip()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        var methodPicker = Sut.ChooseMethod(null);
        var speciesPicker = Sut.ChooseSpecies(null);

        // Assert
        methodPicker.Should().BeFalse();
        speciesPicker.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldAskThePageToOpenTheSpeciesCatalogueWhenMoreIsChosen()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        var more = Sut.SpeciesOptions.Single(option => option.Code == ImportViewModel.MoreOptionCode);

        // Act
        var openPicker = Sut.ChooseSpecies(more);

        // Assert
        openPicker.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldSelectASpeciesChipWithoutOpeningThePicker()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        var species = new ImportOptionModel(SpeciesId, "BROWN", "Brown trout");

        // Act
        var openPicker = Sut.ChooseSpecies(species);

        // Assert
        openPicker.Should().BeFalse();
        Sut.SpeciesOptions.Single(option => option.Id == SpeciesId).IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldSelectAMethodAndNotifyTheDependentState()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        var raised = new List<string?>();
        Sut.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        // Act
        Sut.SelectMethod(OtherMethodId);

        // Assert
        Sut.MethodOptions.Single(option => option.IsSelected).Id.Should().Be(OtherMethodId);
        raised.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ItShouldSelectASpecies()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        Sut.SelectSpecies(SpeciesId);

        // Assert
        Sut.SpeciesOptions.Single(option => option.IsSelected).Id.Should().Be(SpeciesId);
    }
}
