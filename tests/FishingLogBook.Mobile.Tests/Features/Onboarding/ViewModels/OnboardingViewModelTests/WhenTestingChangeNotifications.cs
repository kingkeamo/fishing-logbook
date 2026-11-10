using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Onboarding.Enums;
using FishingLogBook.Mobile.Features.Onboarding.Models;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Onboarding.ViewModels.OnboardingViewModelTests;

public sealed class WhenTestingChangeNotifications : BaseOnboardingViewModelTest
{
    private List<string?> Subscribe()
    {
        var raised = new List<string?>();
        Sut.PropertyChanged += (_, args) => raised.Add(args.PropertyName);
        return raised;
    }

    [Fact]
    public async Task ItShouldReportTheStepAndAvailabilityWhenTheWizardAdvances()
    {
        // Arrange
        var raised = Subscribe();
        await LoadAsync();
        raised.Clear();

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        raised.Should().Contain([
            nameof(Sut.StepNumber),
            nameof(Sut.StepLabel),
            nameof(Sut.StepTitle),
            nameof(Sut.StepDescription),
            nameof(Sut.ContinueText),
            nameof(Sut.IsWelcomeStep),
            nameof(Sut.IsPreferencesStep),
            nameof(Sut.IsLocationStep),
            nameof(Sut.IsFinishStep),
            nameof(Sut.LocationHandled),
            nameof(Sut.CanChooseLocation),
            nameof(Sut.CanGoBack),
            nameof(Sut.CanRetryLoad),
            nameof(Sut.CanContinue),
            nameof(Sut.IsBusy)
        ]);
    }

    [Fact]
    public async Task ItShouldReportMessageChangesOnlyWhenTheMessageActuallyChanges()
    {
        // Arrange
        await ReachPreferencesAsync();
        var spinning = Profile.Methods.Single(method => method.Id == TestAnglerPreferences.SpinningId);
        if (!spinning.IsSelected)
        {
            Profile.ToggleMethod(spinning);
        }

        foreach (var method in Profile.SelectedMethods.ToArray())
        {
            foreach (var species in method.SelectedSpecies.ToArray())
            {
                Profile.RemoveSpecies(method, species);
            }
        }

        var raised = Subscribe();

        // Act
        await Sut.ContinueAsync(CancellationToken.None);
        var firstCount = raised.Count(name => name == nameof(Sut.Message));
        await Sut.ContinueAsync(CancellationToken.None);
        var secondCount = raised.Count(name => name == nameof(Sut.Message));

        // Assert
        firstCount.Should().Be(1);
        secondCount.Should().Be(firstCount);
        Sut.HasMessage.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldRepublishWhenTheProfileAvailabilityChanges()
    {
        // Arrange
        var raised = Subscribe();

        // Act
        await LoadAsync();

        // Assert
        raised.Should().Contain(nameof(Sut.CanContinue));
        raised.Should().Contain(nameof(Sut.CanRetryLoad));
        raised.Should().Contain(nameof(Sut.IsBusy));
    }

    [Fact]
    public void ItShouldRepublishWhenTheCultureChangesAndIgnoreOtherTextChanges()
    {
        // Arrange
        var raised = Subscribe();

        // Act
        Text.SetCulture("fr-FR");

        // Assert
        raised.Should().Contain(nameof(Sut.StepTitle));
    }

    [Fact]
    public async Task ItShouldReportBusyWhileTheLocationIsBeingCaptured()
    {
        // Arrange
        await ReachLocationAsync();
        var raised = Subscribe();
        var entered = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        MockLocation.CaptureCurrentAsync(Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            entered.SetResult();
            await release.Task;
            return DeviceLocationCaptureModel.WithoutCoordinates(LocationCaptureOutcomeEnum.Denied);
        });

        // Act
        var capture = Sut.AllowLocationAsync(CancellationToken.None);
        await entered.Task;
        var busyDuring = Sut.IsBusy;
        var canGoBackDuring = Sut.CanGoBack;
        var canChooseDuring = Sut.CanChooseLocation;
        await Sut.AllowLocationAsync(CancellationToken.None);
        Sut.SkipLocation();
        release.SetResult();
        await capture;

        // Assert
        busyDuring.Should().BeTrue();
        canGoBackDuring.Should().BeFalse();
        canChooseDuring.Should().BeFalse();
        Sut.LocationHandled.Should().BeTrue();
        Sut.Message.Should().Be(Text["OnboardingLocationDenied"]);
        await MockLocation.Received(1).CaptureCurrentAsync(Arg.Any<CancellationToken>());
        raised.Should().Contain(nameof(Sut.CanChooseLocation));
    }

    [Fact]
    public async Task ItShouldTreatAMissingLocationFixAsUnavailable()
    {
        // Arrange
        await ReachLocationAsync();
        MockLocation.CaptureCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DeviceLocationCaptureModel?>(null));

        // Act
        await Sut.AllowLocationAsync(CancellationToken.None);

        // Assert
        Sut.LocationHandled.Should().BeTrue();
        Sut.Message.Should().Be(Text["OnboardingLocationUnavailable"]);
    }

    [Fact]
    public async Task ItShouldIgnoreACaptureWhenADifferentUserIsNowSignedIn()
    {
        // Arrange
        await ReachLocationAsync();
        MockLocation.CaptureCurrentAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            MockAuthentication.CurrentSession.Returns(Session with { UserId = Guid.NewGuid() });
            return DeviceLocationCaptureModel.WithoutCoordinates(LocationCaptureOutcomeEnum.Denied);
        });

        // Act
        await Sut.AllowLocationAsync(CancellationToken.None);

        // Assert
        Sut.LocationHandled.Should().BeFalse();
    }

    [Theory]
    [InlineData(ApiOutcomeEnum.Unavailable, "ProfileOfflineError")]
    [InlineData(ApiOutcomeEnum.SessionExpired, "ProfileSessionExpired")]
    [InlineData(ApiOutcomeEnum.ValidationFailed, "ProfileValidationError")]
    [InlineData(ApiOutcomeEnum.ServerError, "OnboardingSaveFailed")]
    public async Task ItShouldExplainWhyCompletionFailed(ApiOutcomeEnum outcome, string key)
    {
        // Arrange
        await ReachFinishAsync();
        MockProfileClient.CompleteOnboardingAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Failed(outcome));

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.Message.Should().Be(Text[key]);
        MockNavigator.DidNotReceive().ShowAuthenticatedApp();
        await MockOfflinePreparation.DidNotReceive().EnsurePreparedAsync(Arg.Any<ProfileDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotNavigateWhenADifferentUserIsSignedInAfterCompletion()
    {
        // Arrange
        await ReachFinishAsync();
        MockProfileClient.CompleteOnboardingAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            MockAuthentication.CurrentSession.Returns(Session with { UserId = Guid.NewGuid() });
            return ApiResultModel<ProfileDto>.Succeeded(Data.Profile with { OnboardingCompleted = true });
        });

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        MockNavigator.DidNotReceive().ShowAuthenticatedApp();
        MockStatus.DidNotReceive().RememberCompleted(Arg.Any<Guid>());
    }

    [Fact]
    public async Task ItShouldNotNavigateWhenADifferentUserIsSignedInAfterPreparation()
    {
        // Arrange
        await ReachFinishAsync();
        MockProfileClient.CompleteOnboardingAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(Data.Profile with { OnboardingCompleted = true }));
        MockOfflinePreparation.EnsurePreparedAsync(Arg.Any<ProfileDto>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            MockAuthentication.CurrentSession.Returns(Session with { UserId = Guid.NewGuid() });
            return true;
        });

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        MockNavigator.DidNotReceive().ShowAuthenticatedApp();
    }

    [Fact]
    public async Task ItShouldFallBackToTheDefaultMessageWhenTheProfileFailsToLoadWithoutOne()
    {
        // Arrange
        MockEditor.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<FishingLogBook.Mobile.Features.Profile.Models.ProfileEditorDataModel>
                .Failed(ApiOutcomeEnum.ServerError));

        // Act
        await LoadAsync();

        // Assert
        Sut.HasMessage.Should().BeTrue();
        Sut.CanRetryLoad.Should().BeTrue();
        Sut.CanContinue.Should().BeFalse();
    }
}
