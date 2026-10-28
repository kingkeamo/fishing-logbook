using System.Globalization;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Onboarding.Enums;
using FishingLogBook.Mobile.Features.Onboarding.Services;
using FishingLogBook.Mobile.Features.Onboarding.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Navigation;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Onboarding.ViewModels.OnboardingGateViewModelTests;

public sealed class WhenTestingStart
{
    private readonly IOnboardingStatusService _status = Substitute.For<IOnboardingStatusService>();
    private readonly IOnboardingNavigator _navigator = Substitute.For<IOnboardingNavigator>();
    private readonly UiTextService _text = new(CultureInfo.GetCultureInfo("en-GB"));
    private readonly OnboardingGateViewModel _sut;

    public WhenTestingStart()
    {
        _sut = new OnboardingGateViewModel(_status, _navigator, _text);
    }

    [Fact]
    public async Task ItShouldOpenOnboardingForANewAccount()
    {
        // Arrange
        _status.ResolveAsync(Arg.Any<CancellationToken>()).Returns(OnboardingStatusEnum.Incomplete);

        // Act
        await _sut.StartAsync(CancellationToken.None);

        // Assert
        _navigator.Received(1).ShowOnboarding();
        _navigator.DidNotReceive().ShowAuthenticatedApp();
        _sut.HasError.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldOpenTheAuthenticatedAppWhenOnboardingIsAlreadyComplete()
    {
        // Arrange
        _status.ResolveAsync(Arg.Any<CancellationToken>()).Returns(OnboardingStatusEnum.Completed);

        // Act
        await _sut.StartAsync(CancellationToken.None);

        // Assert
        _navigator.Received(1).ShowAuthenticatedApp();
        _navigator.DidNotReceive().ShowOnboarding();
    }

    [Fact]
    public async Task ItShouldShowRetryWhenOnboardingStatusCannotBeDetermined()
    {
        // Arrange
        _status.ResolveAsync(Arg.Any<CancellationToken>()).Returns(OnboardingStatusEnum.Unknown);

        // Act
        await _sut.StartAsync(CancellationToken.None);

        // Assert
        _sut.HasError.Should().BeTrue();
        _sut.CanRetry.Should().BeTrue();
        _sut.Message.Should().Be(_text["OnboardingStatusError"]);
        _navigator.DidNotReceive().ShowOnboarding();
        _navigator.DidNotReceive().ShowAuthenticatedApp();
    }

    [Fact]
    public async Task ItShouldIgnoreASecondStartWhileTheFirstIsInFlight()
    {
        // Arrange
        var gate = new TaskCompletionSource<OnboardingStatusEnum>();
        _status.ResolveAsync(Arg.Any<CancellationToken>()).Returns(_ => gate.Task);

        // Act
        var first = _sut.StartAsync(CancellationToken.None);
        var second = _sut.StartAsync(CancellationToken.None);
        gate.SetResult(OnboardingStatusEnum.Completed);
        await Task.WhenAll(first, second);

        // Assert
        await _status.Received(1).ResolveAsync(Arg.Any<CancellationToken>());
        _navigator.Received(1).ShowAuthenticatedApp();
    }
}
