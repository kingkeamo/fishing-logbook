using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Onboarding.ViewModels.OnboardingViewModelTests;

public sealed class WhenTestingNavigation : BaseOnboardingViewModelTest
{
    [Fact]
    public async Task ItShouldAdvanceThroughFourLinearSteps()
    {
        // Arrange
        await LoadAsync();

        // Act
        Sut.StepNumber.Should().Be(1);
        Sut.CanGoBack.Should().BeFalse();
        await Sut.ContinueAsync(CancellationToken.None);
        Sut.StepNumber.Should().Be(2);
        KeepValidPreferences();
        ArrangeSave(SaveResultModel<ProfileEditorDataModel>.Saved(ValidSavedData()));
        await Sut.ContinueAsync(CancellationToken.None);
        Sut.StepNumber.Should().Be(3);
        Sut.SkipLocation();
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.StepNumber.Should().Be(4);
        Sut.ContinueText.Should().Be(Text["OnboardingFinish"]);
        Sut.CanGoBack.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldKeepTheDisplayNameWhenNavigatingBackAndForward()
    {
        // Arrange
        await LoadAsync();
        Profile.DisplayName = "River angler";

        // Act
        await Sut.ContinueAsync(CancellationToken.None);
        Sut.Back();

        // Assert
        Sut.StepNumber.Should().Be(1);
        Profile.DisplayName.Should().Be("River angler");
    }

    [Fact]
    public async Task ItShouldKeepUnitsAndMethodSelectionsWhenNavigatingBackFromPreferences()
    {
        // Arrange
        await ReachPreferencesAsync();
        Profile.SelectedWeightUnit = Profile.WeightUnitOptions.Single(option => option.Value == WeightUnitEnum.Kg);
        Profile.SelectedLengthUnit = Profile.LengthUnitOptions.Single(option => option.Value == LengthUnitEnum.Cm);
        KeepValidPreferences();

        // Act
        Sut.Back();
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Profile.SelectedWeightUnit!.Value.Should().Be(WeightUnitEnum.Kg);
        Profile.SelectedLengthUnit!.Value.Should().Be(LengthUnitEnum.Cm);
        Profile.SelectedMethods.Should().ContainSingle(method => method.Id == TestAnglerPreferences.FlyId);
        await MockEditor.DidNotReceive().SaveAsync(
            Arg.Any<ProfileEditorDataModel>(),
            Arg.Any<UpdateProfileDto>(),
            Arg.Any<UpdateFishingPreferencesDto>(),
            Arg.Any<UpdateFishingLocationPreferencesDto>(),
            Arg.Any<CancellationToken>());
    }
}
