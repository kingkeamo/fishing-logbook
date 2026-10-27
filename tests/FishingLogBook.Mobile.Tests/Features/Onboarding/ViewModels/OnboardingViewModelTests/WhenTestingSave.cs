using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Onboarding.ViewModels.OnboardingViewModelTests;

public sealed class WhenTestingSave : BaseOnboardingViewModelTest
{
    [Fact]
    public async Task ItShouldSaveTheDisplayNameUnitsAndPreferencesBeforeLeavingThePreferencesStep()
    {
        // Arrange
        await ReachPreferencesAsync();
        Profile.DisplayName = "River angler";
        Profile.SelectedWeightUnit = Profile.WeightUnitOptions.Single(option => option.Value == WeightUnitEnum.Kg);
        Profile.SelectedLengthUnit = Profile.LengthUnitOptions.Single(option => option.Value == LengthUnitEnum.Cm);
        KeepValidPreferences();
        UpdateProfileDto? profile = null;
        UpdateFishingPreferencesDto? preferences = null;
        UpdateFishingLocationPreferencesDto? locations = null;
        MockEditor.SaveAsync(
                Arg.Any<ProfileEditorDataModel>(),
                Arg.Any<UpdateProfileDto>(),
                Arg.Any<UpdateFishingPreferencesDto>(),
                Arg.Any<UpdateFishingLocationPreferencesDto>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                profile = call.Arg<UpdateProfileDto>();
                preferences = call.Arg<UpdateFishingPreferencesDto>();
                locations = call.Arg<UpdateFishingLocationPreferencesDto>();
                return SaveResultModel<ProfileEditorDataModel>.Saved(ValidSavedData());
            });

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.StepNumber.Should().Be(3);
        profile!.DisplayName.Should().Be("River angler");
        profile.PreferredWeightUnit.Should().Be(WeightUnitEnum.Kg);
        profile.PreferredLengthUnit.Should().Be(LengthUnitEnum.Cm);
        profile.HomeRegion.Should().Be("Connemara");
        profile.ShowPreferredFishingMethods.Should().BeFalse();
        profile.ShowPreferredSpecies.Should().BeFalse();
        preferences!.Methods.Should().ContainSingle(method => method.FishingMethodId == TestAnglerPreferences.FlyId);
        locations!.Locations.Select(location => location.Id).Should().Equal(
            TestAnglerPreferences.CorribId,
            TestAnglerPreferences.MoyId);
    }

    [Fact]
    public async Task ItShouldSaveVisibilityFlagsChosenOnTheWelcomeStep()
    {
        // Arrange
        await LoadAsync();
        Profile.ShowPreferredFishingMethods = true;
        Profile.ShowPreferredSpecies = true;
        await Sut.ContinueAsync(CancellationToken.None);
        Sut.Back();
        Profile.ShowPreferredFishingMethods.Should().BeTrue();
        await Sut.ContinueAsync(CancellationToken.None);
        KeepValidPreferences();
        UpdateProfileDto? profile = null;
        MockEditor.SaveAsync(
                Arg.Any<ProfileEditorDataModel>(),
                Arg.Any<UpdateProfileDto>(),
                Arg.Any<UpdateFishingPreferencesDto>(),
                Arg.Any<UpdateFishingLocationPreferencesDto>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                profile = call.Arg<UpdateProfileDto>();
                return SaveResultModel<ProfileEditorDataModel>.Saved(ValidSavedData());
            });

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.StepNumber.Should().Be(3);
        profile!.ShowPreferredFishingMethods.Should().BeTrue();
        profile.ShowPreferredSpecies.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldStayOnPreferencesAndAllowRetryAfterAFailedSave()
    {
        // Arrange
        await ReachPreferencesAsync();
        KeepValidPreferences();
        ArrangeSave(
            SaveResultModel<ProfileEditorDataModel>.WriteFailed(ApiOutcomeEnum.ValidationFailed, earlierWritesSaved: false),
            SaveResultModel<ProfileEditorDataModel>.Saved(ValidSavedData()));

        // Act
        await Sut.ContinueAsync(CancellationToken.None);
        var failedStep = Sut.StepNumber;
        var failedMessage = Sut.Message;
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        failedStep.Should().Be(2);
        failedMessage.Should().Be(Text["ProfileValidationError"]);
        Sut.StepNumber.Should().Be(3);
    }

    [Fact]
    public async Task ItShouldKeepPartialSaveStateUntilRetrySucceeds()
    {
        // Arrange
        await ReachPreferencesAsync();
        KeepValidPreferences();
        ArrangeSave(
            SaveResultModel<ProfileEditorDataModel>.WriteFailed(ApiOutcomeEnum.ServerError, earlierWritesSaved: true),
            SaveResultModel<ProfileEditorDataModel>.Saved(ValidSavedData()));

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.StepNumber.Should().Be(2);
        Profile.IsSaveIncomplete.Should().BeTrue();
        Sut.Message.Should().Be(Text["ProfilePartialSaveError"]);

        await Sut.ContinueAsync(CancellationToken.None);
        Sut.StepNumber.Should().Be(3);
        Profile.IsSaveIncomplete.Should().BeFalse();
    }
}
