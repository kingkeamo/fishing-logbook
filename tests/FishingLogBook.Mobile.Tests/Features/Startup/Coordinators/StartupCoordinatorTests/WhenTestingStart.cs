using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Enums;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using FishingLogBook.Mobile.Features.Startup.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Startup.Coordinators.StartupCoordinatorTests;

public sealed class WhenTestingStart : BaseStartupCoordinatorTest
{
    [Fact]
    public async Task ItShouldRequireOnlineSignInOnAFirstOfflineLaunch()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        // Act
        var result = await CreateSut().StartAsync(CancellationToken.None);
        // Assert
        result.Should().Be(StartupResultEnum.OfflineAccessUnavailable);
        await MockAuthentication.DidNotReceive().InitializeAsync(Arg.Any<CancellationToken>());
        await MockDeviceUnlock.DidNotReceive().UnlockAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        MockOfflineSession.DidNotReceive().Activate(Arg.Any<OfflineCapabilityModel>());
    }

    [Fact]
    public async Task ItShouldRejectAnOfflineLaunchWithoutAReadyCapability()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        MockOfflineCapability.GetReadyAsync(CancellationToken.None).Returns((OfflineCapabilityModel?)null);
        // Act
        var result = await CreateSut().StartAsync(CancellationToken.None);
        // Assert
        result.Should().Be(StartupResultEnum.OfflineAccessUnavailable);
        await MockAccountSession.DidNotReceive().TransitionAsync(
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotActivateOfflineAccessWhenDeviceUnlockIsCancelled()
    {
        // Arrange
        var capability = new OfflineCapabilityModel(Subject, UserId, IsDeviceReady: true);
        MockConnectivity.IsOnline.Returns(false);
        MockOfflineCapability.GetReadyAsync(CancellationToken.None).Returns(capability);
        MockDeviceUnlock.UnlockAsync(Arg.Any<string>(), CancellationToken.None)
            .Returns(DeviceUnlockResultEnum.Cancelled);
        // Act
        var result = await CreateSut().StartAsync(CancellationToken.None);
        // Assert
        result.Should().Be(StartupResultEnum.UnlockCancelled);
        MockOfflineSession.DidNotReceive().Activate(Arg.Any<OfflineCapabilityModel>());
        await MockAccountSession.DidNotReceive().TransitionAsync(
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRestoreAReturningOnlineUser()
    {
        // Arrange
        var session = new AuthenticationSessionModel(
            "access-token",
            DateTimeOffset.UtcNow.AddHours(1),
            "refresh-token",
            Subject,
            "angler@example.com",
            UserId);
        MockConnectivity.IsOnline.Returns(true);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns(AuthenticationResultEnum.Success);
        MockAuthentication.CurrentSession.Returns(session);
        // Act
        var result = await CreateSut().StartAsync(CancellationToken.None);
        // Assert
        result.Should().Be(StartupResultEnum.OnlineSessionReady);
        await MockAuthentication.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
        await MockOfflineCapability.DidNotReceive().GetReadyAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRequireSignInAfterPermanentCognitoRejection()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(true);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns(AuthenticationResultEnum.Invalid);
        // Act
        var result = await CreateSut().StartAsync(CancellationToken.None);
        // Assert
        result.Should().Be(StartupResultEnum.SignInRequired);
        await MockOfflineCapability.DidNotReceive().GetReadyAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowARecoverableErrorWhenTheServiceFailsWhileInternetRemainsAvailable()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(true);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns(AuthenticationResultEnum.TemporarilyUnavailable);
        // Act
        var result = await CreateSut().StartAsync(CancellationToken.None);
        // Assert
        result.Should().Be(StartupResultEnum.ServiceUnavailable);
        await MockOfflineCapability.DidNotReceive().GetReadyAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldUnlockOfflineWhenConnectivityIsLostDuringRestoration()
    {
        // Arrange
        var capability = new OfflineCapabilityModel(Subject, UserId, IsDeviceReady: true);
        MockConnectivity.IsOnline.Returns(true, false);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns(AuthenticationResultEnum.TemporarilyUnavailable);
        MockOfflineCapability.GetReadyAsync(Arg.Any<CancellationToken>()).Returns(capability);
        MockDeviceUnlock.UnlockAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(DeviceUnlockResultEnum.Success);
        // Act
        var result = await CreateSut().StartAsync(CancellationToken.None);
        // Assert
        result.Should().Be(StartupResultEnum.OfflineSessionReady);
        await MockAccountSession.Received(1).TransitionAsync(UserId, Arg.Any<CancellationToken>());
        MockOfflineSession.Received(1).Activate(capability);
    }

    [Fact]
    public async Task ItShouldResolveATimedOutRestorationToARecoverableError()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(true);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, call.Arg<CancellationToken>());
                return AuthenticationResultEnum.Success;
            });
        // Act
        var result = await CreateSut(TimeSpan.FromMilliseconds(20)).StartAsync(CancellationToken.None);
        // Assert
        result.Should().Be(StartupResultEnum.ServiceUnavailable);
        await MockAuthentication.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPropagateCallerCancellationWithoutActivatingAnAccount()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        MockConnectivity.IsOnline.Returns(true);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await cancellation.CancelAsync();
                call.Arg<CancellationToken>().ThrowIfCancellationRequested();
                return AuthenticationResultEnum.Success;
            });
        // Act
        var action = () => CreateSut().StartAsync(cancellation.Token);
        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
        await MockAccountSession.DidNotReceive().TransitionAsync(
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldUnlockOnlyTheCapabilityOwnerWithoutCreatingAnApiSession()
    {
        // Arrange
        var capability = new OfflineCapabilityModel(Subject, UserId, IsDeviceReady: true);
        MockConnectivity.IsOnline.Returns(false);
        MockOfflineCapability.GetReadyAsync(CancellationToken.None).Returns(capability);
        MockDeviceUnlock.UnlockAsync(Arg.Any<string>(), CancellationToken.None)
            .Returns(DeviceUnlockResultEnum.Success);
        // Act
        var result = await CreateSut().StartAsync(CancellationToken.None);
        // Assert
        result.Should().Be(StartupResultEnum.OfflineSessionReady);
        MockAuthentication.CurrentSession.Should().BeNull();
        await MockAuthentication.DidNotReceive().GetAccessTokenAsync(Arg.Any<CancellationToken>());
        await MockAccountSession.Received(1).TransitionAsync(UserId, CancellationToken.None);
        MockOfflineSession.Received(1).Activate(Arg.Is<OfflineCapabilityModel>(value => value.UserId == UserId));
    }
}
