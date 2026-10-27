using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Onboarding.ViewModels.OnboardingViewModelTests;

public sealed class WhenTestingAccountSwitch : BaseOnboardingViewModelTest
{
    [Fact]
    public async Task ItShouldResetTheWizardWhenTheSignedInAccountChanges()
    {
        // Arrange
        await ReachPreferencesAsync();
        Profile.DisplayName = "Halfway";

        // Act
        MockAuthentication.SessionChanged += Raise.Event();

        // Assert
        Sut.StepNumber.Should().Be(1);
        Sut.LocationHandled.Should().BeFalse();
        Sut.CanGoBack.Should().BeFalse();
        Profile.HasContent.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotSaveAfterSignOutDuringPreferenceSubmission()
    {
        // Arrange
        await ReachPreferencesAsync();
        KeepValidPreferences();
        MockEditor.SaveAsync(
                Arg.Any<ProfileEditorDataModel>(),
                Arg.Any<UpdateProfileDto>(),
                Arg.Any<UpdateFishingPreferencesDto>(),
                Arg.Any<UpdateFishingLocationPreferencesDto>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                MockAuthentication.SessionChanged += Raise.Event();
                return SaveResultModel<ProfileEditorDataModel>.NotAttempted(ApiOutcomeEnum.SessionExpired);
            });

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.StepNumber.Should().Be(1);
        MockNavigator.DidNotReceive().ShowAuthenticatedApp();
    }

    [Fact]
    public async Task ItShouldIgnoreAStaleCompletionForAnotherAccount()
    {
        // Arrange
        await ReachFinishAsync();
        MockProfileClient.CompleteOnboardingAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                MockAuthentication.CurrentSession.Returns((AuthenticationSessionModel?)null);
                MockSession.IsCurrent(Lease).Returns(false);
                return ApiResultModel<ProfileDto>.Succeeded(Data.Profile with { OnboardingCompleted = true });
            });

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        MockNavigator.DidNotReceive().ShowAuthenticatedApp();
        MockStatus.DidNotReceive().RememberCompleted(Arg.Any<Guid>());
    }
}
