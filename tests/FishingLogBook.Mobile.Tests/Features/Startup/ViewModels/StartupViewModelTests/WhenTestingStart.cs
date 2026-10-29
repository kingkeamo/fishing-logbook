using System.Globalization;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Startup.Coordinators;
using FishingLogBook.Mobile.Features.Startup.Enums;
using FishingLogBook.Mobile.Features.Startup.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Navigation;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Startup.ViewModels.StartupViewModelTests;

public sealed class WhenTestingStart
{
    private readonly IStartupCoordinator _coordinator = Substitute.For<IStartupCoordinator>();
    private readonly IStartupNavigator _navigator = Substitute.For<IStartupNavigator>();
    private readonly UiTextService _text = new(CultureInfo.GetCultureInfo("en-GB"));

    [Fact]
    public async Task ItShouldShowARecoverableStateWhenOnlineServicesRemainUnavailable()
    {
        // Arrange
        _coordinator.StartAsync(CancellationToken.None).Returns(StartupResultEnum.ServiceUnavailable);
        var sut = CreateSut();
        // Act
        await sut.StartAsync(CancellationToken.None);
        // Assert
        sut.StatusText.Should().Be(_text["StartupServiceUnavailable"]);
        sut.CanRetry.Should().BeTrue();
        _navigator.DidNotReceive().ShowSignIn();
        _navigator.DidNotReceive().ShowOnlineSession();
        _navigator.DidNotReceive().ShowOfflineSession();
    }

    [Fact]
    public async Task ItShouldShowSignInWhenNoOnlineCredentialExists()
    {
        // Arrange
        _coordinator.StartAsync(CancellationToken.None).Returns(StartupResultEnum.SignInRequired);
        var sut = CreateSut();
        // Act
        await sut.StartAsync(CancellationToken.None);
        // Assert
        _navigator.Received(1).ShowSignIn();
        _navigator.DidNotReceive().ShowOnlineSession();
        _navigator.DidNotReceive().ShowOfflineSession();
    }

    [Fact]
    public async Task ItShouldShowOnlyTheOfflineWorkspaceAfterUnlock()
    {
        // Arrange
        _coordinator.StartAsync(CancellationToken.None).Returns(StartupResultEnum.OfflineSessionReady);
        var sut = CreateSut();
        // Act
        await sut.StartAsync(CancellationToken.None);
        // Assert
        _navigator.Received(1).ShowOfflineSession();
        _navigator.DidNotReceive().ShowSignIn();
        _navigator.DidNotReceive().ShowOnlineSession();
    }

    private StartupViewModel CreateSut()
    {
        return new StartupViewModel(
            _coordinator,
            _navigator,
            _text,
            NullLogger<StartupViewModel>.Instance);
    }
}
