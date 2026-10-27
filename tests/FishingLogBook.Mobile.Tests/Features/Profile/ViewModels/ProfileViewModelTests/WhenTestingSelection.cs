using AwesomeAssertions;
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
    public async Task ItShouldSelectAndDefaultTheFirstSpecies()
    {
        // Arrange
        await LoadAsync();
        var spinning = Sut.Methods.Single(method => method.Id == TestAnglerPreferences.SpinningId);
        var salmon = spinning.Species.Single(species => species.Id == TestAnglerPreferences.SalmonId);

        // Act
        Sut.ToggleSpecies(spinning, salmon);

        // Assert
        salmon.IsSelected.Should().BeTrue();
        salmon.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldPromoteAnotherSpeciesWhenTheDefaultIsDeselected()
    {
        // Arrange
        await LoadAsync();
        var fly = Sut.Methods.Single(method => method.Id == TestAnglerPreferences.FlyId);
        var salmon = fly.Species.Single(species => species.Id == TestAnglerPreferences.SalmonId);

        // Act
        Sut.ToggleSpecies(fly, salmon);

        // Assert
        fly.Species.Should().ContainSingle(species => species.IsDefault);
        fly.Species.Single(species => species.IsDefault).Id.Should().Be(TestAnglerPreferences.BrownTroutId);
    }
}
