using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Tests.Support;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Profile.ViewModels.ProfileViewModelTests;

public sealed class WhenTestingLifecycleAndEditing : BaseProfileViewModelTest
{
    [Fact]
    public async Task ItShouldClearTheProfileWhenTheSignedInAccountChanges()
    {
        // Arrange
        await LoadAsync();
        Sut.Methods.Should().NotBeEmpty();
        Sut.Locations.Should().NotBeEmpty();

        // Act
        MockAuthentication.SessionChanged += Raise.Event();

        // Assert
        Sut.HasContent.Should().BeFalse();
        Sut.Methods.Should().BeEmpty();
        Sut.Locations.Should().BeEmpty();
        Sut.CanSave.Should().BeFalse();
        Sut.Message.Should().BeNull();
        Sut.IsSaveIncomplete.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldStopReactingToAccountChangesAfterBeingDetached()
    {
        // Arrange
        await LoadAsync();
        Sut.Detach();

        // Act
        MockAuthentication.SessionChanged += Raise.Event();

        // Assert
        Sut.HasContent.Should().BeTrue();
        Sut.Methods.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ItShouldNotSubscribeTwiceWhenAttachedRepeatedly()
    {
        // Arrange
        await LoadAsync();
        var dispatched = 0;
        Sut.Attach(action =>
        {
            dispatched++;
            action();
        });
        Sut.Attach(action =>
        {
            dispatched++;
            action();
        });

        // Act
        MockAuthentication.SessionChanged += Raise.Event();

        // Assert
        dispatched.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldRefreshAvailabilityWhenConnectivityChanges()
    {
        // Arrange
        await LoadAsync();
        Sut.CanSave.Should().BeTrue();
        MockConnectivity.IsOnline.Returns(false);
        var raised = new List<string?>();
        Sut.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        // Act
        MockConnectivity.ConnectivityChanged += Raise.Event();

        // Assert
        Sut.IsOnline.Should().BeFalse();
        Sut.CanSave.Should().BeFalse();
        raised.Should().Contain([nameof(Sut.IsOnline), nameof(Sut.CanSave)]);
    }

    [Fact]
    public async Task ItShouldRelabelMethodsAndSpeciesWhenTheLanguageChanges()
    {
        // Arrange
        await LoadAsync();
        var fly = Sut.Methods.Single(method => method.Id == TestAnglerPreferences.FlyId);
        var before = fly.Name;

        // Act
        Text.SetCulture("fr-FR");

        // Assert
        Text.Culture.Name.Should().Be("fr-FR");
        fly.Name.Should().NotBe(before);
        fly.DefaultDescription.Should().Contain(fly.Name);
        fly.Species.Should().OnlyContain(species => !string.IsNullOrWhiteSpace(species.Name));
        Sut.Methods.Single(method => method.Id == TestAnglerPreferences.FlyId).Name.Should().Be(fly.Name);
    }

    [Fact]
    public async Task ItShouldReselectAnUnselectedMethodWhenItIsToggled()
    {
        // Arrange
        await LoadAsync();
        var spinning = Sut.Methods.Single(method => method.Id == TestAnglerPreferences.SpinningId);
        Sut.ToggleMethod(spinning);
        spinning.IsSelected.Should().BeFalse();

        // Act
        Sut.ToggleMethod(spinning);

        // Assert
        spinning.IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldChangeTheSingleDefaultSpecies()
    {
        // Arrange
        await LoadAsync();
        var fly = Sut.Methods.Single(method => method.Id == TestAnglerPreferences.FlyId);
        var brown = fly.Species.Single(species => species.Id == TestAnglerPreferences.BrownTroutId);

        // Act
        Sut.SetDefaultSpecies(fly, brown);

        // Assert
        fly.Species.Where(species => species.IsDefault).Should().ContainSingle().Which.Id
            .Should().Be(TestAnglerPreferences.BrownTroutId);
    }

    [Fact]
    public async Task ItShouldOfferEveryMethodInTheMethodPickerWithTheSelectedOnesMarked()
    {
        // Arrange
        await LoadAsync();

        // Act
        var picker = Sut.CreateMethodPicker();

        // Assert
        picker.Options.Select(option => option.Id).Should().BeEquivalentTo(
            [TestAnglerPreferences.FlyId, TestAnglerPreferences.SpinningId]);
        picker.SelectedOptions.Select(option => option.Id).Should().Contain(TestAnglerPreferences.FlyId);
    }

    [Fact]
    public async Task ItShouldOfferTheMethodsSpeciesInTheSpeciesPicker()
    {
        // Arrange
        await LoadAsync();
        var fly = Sut.Methods.Single(method => method.Id == TestAnglerPreferences.FlyId);

        // Act
        var picker = Sut.CreateSpeciesPicker(fly);

        // Assert
        picker.Options.Select(option => option.Id).Should().BeEquivalentTo(
            [TestAnglerPreferences.SalmonId, TestAnglerPreferences.BrownTroutId]);
        picker.Title.Should().Contain(fly.Name);
    }

    [Fact]
    public async Task ItShouldApplyAMethodPickerChoiceBySelectingAndDeselectingMethods()
    {
        // Arrange
        await LoadAsync();

        // Act
        Sut.ApplyMethodPicker(new CataloguePickerResultModel(new HashSet<Guid> { TestAnglerPreferences.SpinningId }));

        // Assert
        Sut.Methods.Single(method => method.Id == TestAnglerPreferences.SpinningId).IsSelected.Should().BeTrue();
        Sut.Methods.Single(method => method.Id == TestAnglerPreferences.FlyId).IsSelected.Should().BeFalse();
        Sut.Methods.Should().ContainSingle(method => method.IsDefault)
            .Which.Id.Should().Be(TestAnglerPreferences.SpinningId);
    }

    [Fact]
    public async Task ItShouldIgnoreASpeciesPickerChoiceForAMethodThatIsNotSelected()
    {
        // Arrange
        await LoadAsync();
        var spinning = Sut.Methods.Single(method => method.Id == TestAnglerPreferences.SpinningId);
        Sut.ToggleMethod(spinning);

        // Act
        Sut.ApplySpeciesPicker(spinning, new CataloguePickerResultModel(new HashSet<Guid> { TestAnglerPreferences.SalmonId }));

        // Assert
        spinning.Species.Should().OnlyContain(species => !species.IsSelected);
    }

    [Fact]
    public async Task ItShouldClearDefaultsFromSpeciesDeselectedInThePicker()
    {
        // Arrange
        await LoadAsync();
        var fly = Sut.Methods.Single(method => method.Id == TestAnglerPreferences.FlyId);

        // Act
        Sut.ApplySpeciesPicker(fly, new CataloguePickerResultModel(new HashSet<Guid> { TestAnglerPreferences.BrownTroutId }));

        // Assert
        fly.Species.Single(species => species.Id == TestAnglerPreferences.SalmonId).IsDefault.Should().BeFalse();
        fly.Species.Single(species => species.Id == TestAnglerPreferences.BrownTroutId).IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldIgnoreRemovingASpeciesThatIsNotSelected()
    {
        // Arrange
        await LoadAsync();
        var spinning = Sut.Methods.Single(method => method.Id == TestAnglerPreferences.SpinningId);
        var salmon = spinning.Species.Single(species => species.Id == TestAnglerPreferences.SalmonId);

        // Act
        Sut.RemoveSpecies(spinning, salmon);

        // Assert
        salmon.IsSelected.Should().BeFalse();
    }
}
