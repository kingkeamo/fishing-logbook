using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Tests.Support;

namespace FishingLogBook.Mobile.Tests.Features.Profile.ViewModels.ProfileViewModelTests;

public sealed class WhenTestingSelection : BaseProfileViewModelTest
{
    [Fact]
    public async Task ItShouldPromoteTheRemainingMethodWhenTheDefaultIsDeselected()
    {
        // Arrange
        await LoadAsync();
        var fly = Sut.Methods.Single(method => method.IsDefault);

        // Act
        Sut.ToggleMethod(fly);

        // Assert
        Sut.Methods.Single(method => method.IsDefault).Id.Should().Be(TestAnglerPreferences.SpinningId);
        fly.Species.Should().OnlyContain(species => !species.IsSelected && !species.IsDefault);
    }

    [Fact]
    public async Task ItShouldChangeTheSingleDefaultMethod()
    {
        // Arrange
        await LoadAsync();
        var spinning = Sut.Methods.Single(method => method.Id == TestAnglerPreferences.SpinningId);

        // Act
        Sut.SetDefaultMethod(spinning);

        // Assert
        Sut.Methods.Should().ContainSingle(method => method.IsDefault);
        spinning.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldSelectAndDefaultTheFirstSpeciesChosenInThePicker()
    {
        // Arrange
        await LoadAsync();
        var spinning = Sut.Methods.Single(method => method.Id == TestAnglerPreferences.SpinningId);

        // Act
        Sut.ApplySpeciesPicker(spinning, new CataloguePickerResultModel(new HashSet<Guid> { TestAnglerPreferences.SalmonId }));

        // Assert
        var salmon = spinning.SelectedSpecies.Should().ContainSingle().Subject;
        salmon.Id.Should().Be(TestAnglerPreferences.SalmonId);
        salmon.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldPromoteAnotherSpeciesWhenTheDefaultIsRemoved()
    {
        // Arrange
        await LoadAsync();
        var fly = Sut.Methods.Single(method => method.Id == TestAnglerPreferences.FlyId);
        var salmon = fly.Species.Single(species => species.Id == TestAnglerPreferences.SalmonId);

        // Act
        Sut.RemoveSpecies(fly, salmon);

        // Assert
        salmon.IsSelected.Should().BeFalse();
        fly.SelectedSpecies.Should().ContainSingle(species => species.IsDefault)
            .Which.Id.Should().Be(TestAnglerPreferences.BrownTroutId);
    }

    [Fact]
    public async Task ItShouldOnlyShowSelectedSpeciesForAMethod()
    {
        // Arrange
        await LoadAsync();
        var fly = Sut.Methods.Single(method => method.Id == TestAnglerPreferences.FlyId);

        // Act
        Sut.RemoveSpecies(fly, fly.Species.Single(species => species.Id == TestAnglerPreferences.BrownTroutId));

        // Assert
        fly.SelectedSpecies.Should().ContainSingle().Which.Id.Should().Be(TestAnglerPreferences.SalmonId);
        fly.Species.Should().HaveCount(2);
    }

    [Fact]
    public async Task ItShouldListSelectedMethodsFirstInTheVisibleChips()
    {
        // Arrange
        await LoadAsync();
        var fly = Sut.Methods.Single(method => method.Id == TestAnglerPreferences.FlyId);

        // Act
        Sut.ToggleMethod(fly);

        // Assert
        Sut.MethodChips.Select(method => method.Id)
            .Should().Equal(TestAnglerPreferences.SpinningId, TestAnglerPreferences.FlyId);
        Sut.SelectedMethods.Should().ContainSingle().Which.Id.Should().Be(TestAnglerPreferences.SpinningId);
    }

    [Fact]
    public async Task ItShouldEndTheMethodChipsWithTheMoreAction()
    {
        // Arrange
        await LoadAsync();

        // Act
        var items = Sut.MethodChipItems;

        // Assert
        items.Should().HaveCount(Sut.MethodChips.Count + 1);
        items[^1].Should().BeOfType<PreferenceMoreActionModel>().Which.Method.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldEndEachMethodsSpeciesChipsWithItsMoreAction()
    {
        // Arrange
        await LoadAsync();
        var fly = Sut.Methods.Single(method => method.Id == TestAnglerPreferences.FlyId);
        var changes = new List<string?>();
        fly.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        // Act
        Sut.RemoveSpecies(fly, fly.SelectedSpecies[0]);

        // Assert
        fly.SpeciesChipItems.Should().HaveCount(fly.SelectedSpecies.Count + 1);
        fly.SpeciesChipItems[^1].Should().BeOfType<PreferenceMoreActionModel>().Which.Method.Should().BeSameAs(fly);
        changes.Should().Contain(nameof(MethodPreferenceEditModel.SpeciesChipItems));
    }

    [Fact]
    public async Task ItShouldKeepTheExistingDefaultMethodWhenThePickerKeepsIt()
    {
        // Arrange
        await LoadAsync();

        // Act
        Sut.ApplyMethodPicker(new CataloguePickerResultModel(
            new HashSet<Guid> { TestAnglerPreferences.FlyId, TestAnglerPreferences.SpinningId }));

        // Assert
        Sut.SelectedMethods.Should().ContainSingle(method => method.IsDefault)
            .Which.Id.Should().Be(TestAnglerPreferences.FlyId);
        Sut.Methods.Single(method => method.Id == TestAnglerPreferences.FlyId).SelectedSpecies.Should().HaveCount(2);
    }

    [Fact]
    public async Task ItShouldApplyTheCompleteMethodSelectionFromThePicker()
    {
        // Arrange
        await LoadAsync();

        // Act
        Sut.ApplyMethodPicker(new CataloguePickerResultModel(new HashSet<Guid> { TestAnglerPreferences.SpinningId }));

        // Assert
        Sut.SelectedMethods.Should().ContainSingle().Which.Id.Should().Be(TestAnglerPreferences.SpinningId);
        Sut.SelectedMethods.Single().IsDefault.Should().BeTrue();
        Sut.Methods.Single(method => method.Id == TestAnglerPreferences.FlyId).Species
            .Should().OnlyContain(species => !species.IsSelected && !species.IsDefault);
    }

    [Fact]
    public async Task ItShouldApplySpeciesPickerSelectionAndPromoteADefault()
    {
        // Arrange
        await LoadAsync();
        var fly = Sut.Methods.Single(method => method.Id == TestAnglerPreferences.FlyId);

        // Act
        Sut.ApplySpeciesPicker(fly, new CataloguePickerResultModel(new HashSet<Guid> { TestAnglerPreferences.BrownTroutId }));

        // Assert
        fly.SelectedSpecies.Should().ContainSingle().Which.Id.Should().Be(TestAnglerPreferences.BrownTroutId);
        fly.SelectedSpecies.Single().IsDefault.Should().BeTrue();
    }
}
