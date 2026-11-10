using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Tests.Support;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Onboarding.ViewModels.OnboardingViewModelTests;

public sealed class WhenTestingStepContentAndGuards : BaseOnboardingViewModelTest
{
    [Fact]
    public async Task ItShouldShowTheWelcomeContentFirst()
    {
        // Arrange
        // Act
        await LoadAsync();

        // Assert
        Sut.IsWelcomeStep.Should().BeTrue();
        Sut.IsPreferencesStep.Should().BeFalse();
        Sut.StepTitle.Should().Be(Text["OnboardingWelcomeTitle"]);
        Sut.StepDescription.Should().Be(Text["OnboardingWelcomeBody"]);
        Sut.ContinueText.Should().Be(Text["OnboardingContinue"]);
        Sut.CanGoBack.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldShowThePreferencesContentSecond()
    {
        // Arrange
        // Act
        await ReachPreferencesAsync();

        // Assert
        Sut.IsPreferencesStep.Should().BeTrue();
        Sut.StepTitle.Should().Be(Text["OnboardingPreferencesTitle"]);
        Sut.StepDescription.Should().Be(Text["OnboardingPreferencesBody"]);
        Sut.CanGoBack.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldShowTheLocationContentThird()
    {
        // Arrange
        // Act
        await ReachLocationAsync();

        // Assert
        Sut.IsLocationStep.Should().BeTrue();
        Sut.StepTitle.Should().Be(Text["OnboardingLocationTitle"]);
        Sut.StepDescription.Should().Be(Text["OnboardingLocationBody"]);
        Sut.CanChooseLocation.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldShowTheFinishContentAndFinishActionLast()
    {
        // Arrange
        // Act
        await ReachFinishAsync();

        // Assert
        Sut.IsFinishStep.Should().BeTrue();
        Sut.StepTitle.Should().Be(Text["OnboardingFinishTitle"]);
        Sut.StepDescription.Should().Be(Text["OnboardingFinishBody"]);
        Sut.ContinueText.Should().Be(Text["OnboardingFinish"]);
        Sut.LocationHandled.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldTranslateTheStepContentWhenTheLanguageChanges()
    {
        // Arrange
        await LoadAsync();
        var english = Sut.StepTitle;

        // Act
        Text.SetCulture("fr-FR");

        // Assert
        Sut.StepTitle.Should().Be(Text["OnboardingWelcomeTitle"]);
        Sut.StepTitle.Should().NotBe(english);
    }

    [Fact]
    public async Task ItShouldNotGoBackFromTheFirstStep()
    {
        // Arrange
        await LoadAsync();

        // Act
        Sut.Back();

        // Assert
        Sut.StepNumber.Should().Be(1);
        Sut.IsWelcomeStep.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldClearTheMessageWhenGoingBack()
    {
        // Arrange
        await ReachPreferencesAsync();
        await Sut.ContinueAsync(CancellationToken.None);
        Sut.Message.Should().NotBeNull();

        // Act
        Sut.Back();

        // Assert
        Sut.IsWelcomeStep.Should().BeTrue();
        Sut.Message.Should().BeNull();
        Sut.HasMessage.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNameEveryMethodThatStillNeedsSpecies()
    {
        // Arrange
        await ReachPreferencesAsync();
        var fly = Profile.Methods.Single(method => method.Id == TestAnglerPreferences.FlyId);
        foreach (var species in fly.Species.Where(item => item.IsSelected).ToArray())
        {
            Profile.RemoveSpecies(fly, species);
        }

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.StepNumber.Should().Be(2);
        Sut.Message.Should().Be(string.Format(
            Text.Culture,
            Text["OnboardingValidationSpeciesForMethods"],
            "Fly, Spinning"));
        await MockEditor.DidNotReceive().SaveAsync(
            Arg.Any<FishingLogBook.Mobile.Features.Profile.Models.ProfileEditorDataModel>(),
            Arg.Any<FishingLogBook.Shared.Dtos.UpdateProfileDto>(),
            Arg.Any<FishingLogBook.Shared.Dtos.UpdateFishingPreferencesDto>(),
            Arg.Any<FishingLogBook.Shared.Dtos.UpdateFishingLocationPreferencesDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportAnExpiredSessionWhenFinishingWithoutALease()
    {
        // Arrange
        await ReachFinishAsync();
        MockSession.Capture().Returns((AccountSessionLeaseModel?)null);

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.StepNumber.Should().Be(4);
        Sut.Message.Should().Be(Text["ProfileSessionExpired"]);
        await MockProfileClient.DidNotReceive().CompleteOnboardingAsync(Arg.Any<CancellationToken>());
        MockNavigator.DidNotReceive().ShowAuthenticatedApp();
    }

    [Fact]
    public async Task ItShouldResetTheWizardWhenCompletionIsCancelledByAnAccountSwitch()
    {
        // Arrange
        await ReachFinishAsync();
        MockProfileClient.CompleteOnboardingAsync(Arg.Any<CancellationToken>())
            .Returns<Task<FishingLogBook.Mobile.Api.Models.ApiResultModel<FishingLogBook.Shared.Dtos.ProfileDto>>>(
                _ => throw new OperationCanceledException());

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        MockNavigator.DidNotReceive().ShowAuthenticatedApp();
        Sut.IsBusy.Should().BeFalse();
        Sut.IsWelcomeStep.Should().BeTrue();
    }
}
