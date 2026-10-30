using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Onboarding.ViewModels.OnboardingViewModelTests;

public sealed class WhenTestingCompletion : BaseOnboardingViewModelTest
{
    [Fact]
    public async Task ItShouldStayOnFinishWhenOfflinePreparationFails()
    {
        // Arrange
        await ReachFinishAsync();
        var completed = Data.Profile with { OnboardingCompleted = true };
        MockProfileClient.CompleteOnboardingAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(completed));
        MockOfflinePreparation.EnsurePreparedAsync(completed, CancellationToken.None).Returns(false);

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.StepNumber.Should().Be(4);
        Sut.Message.Should().Be(Text["OnboardingPreparationError"]);
        MockStatus.DidNotReceive().RememberCompleted(Arg.Any<Guid>());
        MockNavigator.DidNotReceive().ShowAuthenticatedApp();
    }

    [Fact]
    public async Task ItShouldEnterTheAppOnlyAfterTheServerConfirmsCompletion()
    {
        // Arrange
        await ReachFinishAsync();
        MockProfileClient.CompleteOnboardingAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(Data.Profile with { OnboardingCompleted = true }));

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        MockStatus.Received(1).RememberCompleted(UserId);
        await MockOfflinePreparation.Received(1).EnsurePreparedAsync(
            Arg.Is<ProfileDto>(profile => profile.UserId == UserId && profile.OnboardingCompleted),
            CancellationToken.None);
        MockNavigator.Received(1).ShowAuthenticatedApp();
        MockNavigator.DidNotReceive().ShowOnboarding();
    }

    [Fact]
    public async Task ItShouldStayOnFinishWhenTheServerDoesNotMarkOnboardingComplete()
    {
        // Arrange
        await ReachFinishAsync();
        MockProfileClient.CompleteOnboardingAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(Data.Profile with { OnboardingCompleted = false }));

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.StepNumber.Should().Be(4);
        Sut.Message.Should().Be(Text["OnboardingSaveFailed"]);
        MockStatus.DidNotReceive().RememberCompleted(Arg.Any<Guid>());
        MockNavigator.DidNotReceive().ShowAuthenticatedApp();
    }

    [Fact]
    public async Task ItShouldAllowRetryAfterAFailedCompletion()
    {
        // Arrange
        await ReachFinishAsync();
        MockProfileClient.CompleteOnboardingAsync(Arg.Any<CancellationToken>())
            .Returns(
                ApiResultModel<ProfileDto>.Failed(ApiOutcomeEnum.Unavailable),
                ApiResultModel<ProfileDto>.Succeeded(Data.Profile with { OnboardingCompleted = true }));

        // Act
        await Sut.ContinueAsync(CancellationToken.None);
        Sut.Message.Should().Be(Text["ProfileOfflineError"]);
        Sut.StepNumber.Should().Be(4);
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        MockNavigator.Received(1).ShowAuthenticatedApp();
        MockStatus.Received(1).RememberCompleted(UserId);
    }

    [Fact]
    public async Task ItShouldIgnoreASecondFinishTapWhileCompletionIsInFlight()
    {
        // Arrange
        await ReachFinishAsync();
        var gate = new TaskCompletionSource<ApiResultModel<ProfileDto>>();
        MockProfileClient.CompleteOnboardingAsync(Arg.Any<CancellationToken>()).Returns(_ => gate.Task);

        // Act
        var first = Sut.ContinueAsync(CancellationToken.None);
        var second = Sut.ContinueAsync(CancellationToken.None);
        gate.SetResult(ApiResultModel<ProfileDto>.Succeeded(Data.Profile with { OnboardingCompleted = true }));
        await Task.WhenAll(first, second);

        // Assert
        await MockProfileClient.Received(1).CompleteOnboardingAsync(Arg.Any<CancellationToken>());
        MockNavigator.Received(1).ShowAuthenticatedApp();
    }

    [Fact]
    public async Task ItShouldNotEnterTheAppWhenAuthenticationHasExpired()
    {
        // Arrange
        await ReachFinishAsync();
        MockProfileClient.CompleteOnboardingAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Failed(ApiOutcomeEnum.SessionExpired));

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.StepNumber.Should().Be(4);
        Sut.Message.Should().Be(Text["ProfileSessionExpired"]);
        MockNavigator.DidNotReceive().ShowAuthenticatedApp();
        MockStatus.DidNotReceive().RememberCompleted(Arg.Any<Guid>());
    }

    [Fact]
    public async Task ItShouldNotNavigateAfterTheAccountChangesDuringCompletion()
    {
        // Arrange
        await ReachFinishAsync();
        MockProfileClient.CompleteOnboardingAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(Data.Profile with { OnboardingCompleted = true }));
        MockSession.IsCurrent(Lease).Returns(false);

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        MockNavigator.DidNotReceive().ShowAuthenticatedApp();
        MockStatus.DidNotReceive().RememberCompleted(Arg.Any<Guid>());
        Sut.StepNumber.Should().Be(4);
    }

    [Fact]
    public async Task ItShouldNotNavigateWhenTheAccountChangesDuringOfflinePreparation()
    {
        // Arrange
        await ReachFinishAsync();
        var completed = Data.Profile with { OnboardingCompleted = true };
        MockProfileClient.CompleteOnboardingAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<ProfileDto>.Succeeded(completed));
        MockOfflinePreparation.EnsurePreparedAsync(completed, CancellationToken.None)
            .Returns(_ =>
            {
                MockSession.IsCurrent(Lease).Returns(false);
                return true;
            });

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        MockStatus.DidNotReceive().RememberCompleted(Arg.Any<Guid>());
        MockNavigator.DidNotReceive().ShowAuthenticatedApp();
    }
}
