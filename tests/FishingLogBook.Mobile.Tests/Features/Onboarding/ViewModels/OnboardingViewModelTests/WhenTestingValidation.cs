using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Onboarding.ViewModels.OnboardingViewModelTests;

public sealed class WhenTestingValidation : BaseOnboardingViewModelTest
{
    [Fact]
    public async Task ItShouldRequireAtLeastOneFishingMethod()
    {
        // Arrange
        MockEditor.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileEditorDataModel>.Succeeded(
                Data with { Preferences = TestAnglerPreferences.Empty() }));
        await ReachPreferencesAsync();

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.StepNumber.Should().Be(2);
        Sut.Message.Should().Be(Text["OnboardingValidationFishingMethod"]);
        await MockEditor.DidNotReceive().SaveAsync(
            Arg.Any<ProfileEditorDataModel>(),
            Arg.Any<UpdateProfileDto>(),
            Arg.Any<UpdateFishingPreferencesDto>(),
            Arg.Any<UpdateFishingLocationPreferencesDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRequireSpeciesForEachSelectedMethod()
    {
        // Arrange
        await ReachPreferencesAsync();

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.StepNumber.Should().Be(2);
        Sut.Message.Should().Be(string.Format(
            Text.Culture,
            Text["OnboardingValidationSpeciesForMethod"],
            "Spinning"));
    }

    [Fact]
    public async Task ItShouldKeepIndependentSpeciesAndDefaultsForEachMethod()
    {
        // Arrange
        await ReachPreferencesAsync();
        var spinning = Profile.Methods.Single(method => method.Id == TestAnglerPreferences.SpinningId);
        var trout = spinning.Species.Single(species => species.Id == TestAnglerPreferences.BrownTroutId);
        spinning.Species.Single(species => species.Id == TestAnglerPreferences.SalmonId).IsSelected = true;
        trout.IsSelected = true;
        Profile.SetDefaultSpecies(spinning, trout);

        // Act
        var fly = Profile.Methods.Single(method => method.Id == TestAnglerPreferences.FlyId);
        fly.IsDefault.Should().BeTrue();
        spinning.IsDefault.Should().BeFalse();

        // Assert
        fly.SelectedSpecies.Single(species => species.IsDefault).Id.Should().Be(TestAnglerPreferences.SalmonId);
        spinning.SelectedSpecies.Single(species => species.IsDefault).Id.Should().Be(TestAnglerPreferences.BrownTroutId);
    }
}
