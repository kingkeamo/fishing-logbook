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
    public async Task ItShouldRequireSignInWhenConnectivityReturnsButNoOnlineCredentialExists()
    {
        // Arrange
        var capability = new OfflineCapabilityModel(Subject, UserId, IsDeviceReady: true);
        MockConnectivity.IsOnline.Returns(false, true);
        MockOfflineCapability.GetReadyAsync(CancellationToken.None).Returns(capability);
        MockDeviceUnlock.UnlockAsync(Text["OfflineUnlockReason"], CancellationToken.None).Returns(DeviceUnlockResultEnum.Success);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>()).Returns(AuthenticationResultEnum.Success);

        // Act
        var result = await CreateSut().StartAsync(CancellationToken.None);

        // Assert
        result.Should().Be(StartupResultEnum.SignInRequired);
        await MockAuthentication.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
        MockOfflineSession.DidNotReceive().Activate(Arg.Any<OfflineCapabilityModel>());
    }

    [Fact]
    public async Task ItShouldRequireSignInWhenNoStoredOnlineSessionExists()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(true);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>()).Returns(AuthenticationResultEnum.Success);

        // Act
        var result = await CreateSut().StartAsync(CancellationToken.None);

        // Assert
        result.Should().Be(StartupResultEnum.SignInRequired);
        await MockAuthentication.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
        await MockOfflineCapability.DidNotReceive().GetReadyAsync(Arg.Any<CancellationToken>());
        MockOfflineSession.DidNotReceive().Activate(Arg.Any<OfflineCapabilityModel>());
    }

    [Fact]
    public async Task ItShouldDenyAnOnlineOutageFallbackWithoutAPreparedCapability()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(true);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>()).Returns(AuthenticationResultEnum.TemporarilyUnavailable);

        // Act
        var result = await CreateSut().StartAsync(CancellationToken.None);

        // Assert
        result.Should().Be(StartupResultEnum.OfflineAccessUnavailable);
        await MockOfflineCapability.Received(1).GetReadyAsync(CancellationToken.None);
        await MockDeviceUnlock.DidNotReceive().UnlockAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await MockAccountSession.DidNotReceive().TransitionAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        MockOfflineSession.DidNotReceive().Activate(Arg.Any<OfflineCapabilityModel>());
    }

    [Fact]
    public async Task ItShouldRecheckPreparedAccessAfterUnlockBeforeActivatingTheAccount()
    {
        // Arrange
        var capability = new OfflineCapabilityModel(Subject, UserId, IsDeviceReady: true);
        MockConnectivity.IsOnline.Returns(true);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>()).Returns(AuthenticationResultEnum.TemporarilyUnavailable);
        MockOfflineCapability.GetReadyAsync(CancellationToken.None).Returns(capability);
        MockOfflinePreparation.IsPreparedAsync(capability, CancellationToken.None).Returns(true, false);
        MockDeviceUnlock.UnlockAsync(Text["OfflineUnlockReason"], CancellationToken.None).Returns(DeviceUnlockResultEnum.Success);

        // Act
        var result = await CreateSut().StartAsync(CancellationToken.None);

        // Assert
        result.Should().Be(StartupResultEnum.OfflineAccessUnavailable);
        await MockOfflinePreparation.Received(2).IsPreparedAsync(capability, CancellationToken.None);
        await MockAccountSession.DidNotReceive().TransitionAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        MockOfflineSession.DidNotReceive().Activate(Arg.Any<OfflineCapabilityModel>());
    }

    [Theory]
    [InlineData(AuthenticationResultEnum.Invalid, StartupResultEnum.SignInRequired)]
    [InlineData(AuthenticationResultEnum.Pending, StartupResultEnum.SignInRequired)]
    [InlineData(AuthenticationResultEnum.Cancelled, StartupResultEnum.SignInRequired)]
    [InlineData(AuthenticationResultEnum.StorageUnavailable, StartupResultEnum.ServiceUnavailable)]
    public async Task ItShouldNotBypassANonTransientFailureWhenConnectivityReturnsDuringUnlock(AuthenticationResultEnum authenticationResult, StartupResultEnum expected)
    {
        // Arrange
        var capability = new OfflineCapabilityModel(Subject, UserId, IsDeviceReady: true);
        MockConnectivity.IsOnline.Returns(false, true);
        MockOfflineCapability.GetReadyAsync(CancellationToken.None).Returns(capability);
        MockDeviceUnlock.UnlockAsync(Text["OfflineUnlockReason"], CancellationToken.None).Returns(DeviceUnlockResultEnum.Success);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>()).Returns(authenticationResult);

        // Act
        var result = await CreateSut().StartAsync(CancellationToken.None);

        // Assert
        result.Should().Be(expected);
        await MockAuthentication.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
        await MockAccountSession.DidNotReceive().TransitionAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        MockOfflineSession.DidNotReceive().Activate(Arg.Any<OfflineCapabilityModel>());
    }

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ItShouldRejectAReadyCapabilityWhenEssentialCachedDataIsMissing(bool isOnline)
    {
        // Arrange
        var capability = new OfflineCapabilityModel(Subject, UserId, IsDeviceReady: true);
        MockConnectivity.IsOnline.Returns(isOnline);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>()).Returns(AuthenticationResultEnum.TemporarilyUnavailable);
        MockOfflineCapability.GetReadyAsync(CancellationToken.None).Returns(capability);
        MockOfflinePreparation.IsPreparedAsync(capability, CancellationToken.None).Returns(false);

        // Act
        var result = await CreateSut().StartAsync(CancellationToken.None);

        // Assert
        result.Should().Be(StartupResultEnum.OfflineAccessUnavailable);
        await MockDeviceUnlock.DidNotReceive().UnlockAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        MockOfflineSession.DidNotReceive().Activate(Arg.Any<OfflineCapabilityModel>());
    }

    [Theory]
    [InlineData(DeviceUnlockResultEnum.Cancelled, StartupResultEnum.UnlockCancelled)]
    [InlineData(DeviceUnlockResultEnum.Failed, StartupResultEnum.UnlockFailed)]
    [InlineData(DeviceUnlockResultEnum.Unavailable, StartupResultEnum.UnlockFailed)]
    public async Task ItShouldNotActivateOfflineAccessWhenDeviceUnlockDoesNotSucceed(DeviceUnlockResultEnum unlock, StartupResultEnum expected)
    {
        // Arrange
        var capability = new OfflineCapabilityModel(Subject, UserId, IsDeviceReady: true);
        MockConnectivity.IsOnline.Returns(true);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>()).Returns(AuthenticationResultEnum.TemporarilyUnavailable);
        MockOfflineCapability.GetReadyAsync(CancellationToken.None).Returns(capability);
        MockDeviceUnlock.UnlockAsync(Arg.Any<string>(), CancellationToken.None)
            .Returns(unlock);
        // Act
        var result = await CreateSut().StartAsync(CancellationToken.None);
        // Assert
        result.Should().Be(expected);
        MockOfflineSession.DidNotReceive().Activate(Arg.Any<OfflineCapabilityModel>());
        await MockAccountSession.DidNotReceive().TransitionAsync(
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRestoreOnlineInsteadOfActivatingOfflineWhenConnectivityReturnsDuringUnlock()
    {
        // Arrange
        var capability = new OfflineCapabilityModel(Subject, UserId, IsDeviceReady: true);
        var session = new AuthenticationSessionModel(
            "access-token",
            DateTimeOffset.UtcNow.AddHours(1),
            "refresh-token",
            Subject,
            "angler@example.com",
            UserId);
        MockConnectivity.IsOnline.Returns(false, true);
        MockOfflineCapability.GetReadyAsync(CancellationToken.None).Returns(capability);
        MockDeviceUnlock.UnlockAsync(Arg.Any<string>(), CancellationToken.None)
            .Returns(DeviceUnlockResultEnum.Success);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns(AuthenticationResultEnum.Success);
        MockAuthentication.CurrentSession.Returns(session);
        // Act
        var result = await CreateSut().StartAsync(CancellationToken.None);
        // Assert
        result.Should().Be(StartupResultEnum.OnlineSessionReady);
        await MockAuthentication.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
        MockOfflineSession.DidNotReceive().Activate(Arg.Any<OfflineCapabilityModel>());
        await MockAccountSession.DidNotReceive().TransitionAsync(
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldActivateOfflineWhenOnlineRestorationFailsAfterUnlock()
    {
        // Arrange
        var capability = new OfflineCapabilityModel(Subject, UserId, IsDeviceReady: true);
        MockConnectivity.IsOnline.Returns(false, true);
        MockOfflineCapability.GetReadyAsync(CancellationToken.None).Returns(capability);
        MockDeviceUnlock.UnlockAsync(Arg.Any<string>(), CancellationToken.None)
            .Returns(DeviceUnlockResultEnum.Success);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns(AuthenticationResultEnum.TemporarilyUnavailable);
        // Act
        var result = await CreateSut().StartAsync(CancellationToken.None);
        // Assert
        result.Should().Be(StartupResultEnum.OfflineSessionReady);
        await MockAuthentication.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
        await MockAccountSession.Received(1).TransitionAsync(UserId, CancellationToken.None);
        MockOfflineSession.Received(1).Activate(capability);
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

    [Theory]
    [InlineData(AuthenticationResultEnum.Invalid, StartupResultEnum.SignInRequired)]
    [InlineData(AuthenticationResultEnum.Pending, StartupResultEnum.SignInRequired)]
    [InlineData(AuthenticationResultEnum.Cancelled, StartupResultEnum.SignInRequired)]
    [InlineData(AuthenticationResultEnum.StorageUnavailable, StartupResultEnum.ServiceUnavailable)]
    [InlineData((AuthenticationResultEnum)99, StartupResultEnum.ServiceUnavailable)]
    public async Task ItShouldNotFallBackForNonTransientRestorationResults(AuthenticationResultEnum authenticationResult, StartupResultEnum expected)
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(true);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns(authenticationResult);
        // Act
        var result = await CreateSut().StartAsync(CancellationToken.None);
        // Assert
        result.Should().Be(expected);
        await MockOfflineCapability.DidNotReceive().GetReadyAsync(Arg.Any<CancellationToken>());
        await MockDeviceUnlock.DidNotReceive().UnlockAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        MockOfflineSession.DidNotReceive().Activate(Arg.Any<OfflineCapabilityModel>());
    }

    [Fact]
    public async Task ItShouldUnlockPreparedOfflineAccessWhenTheServiceFailsWhileInternetRemainsAvailable()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(true);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns(AuthenticationResultEnum.TemporarilyUnavailable);
        var capability = new OfflineCapabilityModel(Subject, UserId, IsDeviceReady: true);
        MockOfflineCapability.GetReadyAsync(CancellationToken.None).Returns(capability);
        MockDeviceUnlock.UnlockAsync(Text["OfflineUnlockReason"], CancellationToken.None)
            .Returns(DeviceUnlockResultEnum.Success);
        // Act
        var result = await CreateSut().StartAsync(CancellationToken.None);
        // Assert
        result.Should().Be(StartupResultEnum.OfflineSessionReady);
        await MockAuthentication.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
        await MockDeviceUnlock.Received(1).UnlockAsync(Text["OfflineUnlockReason"], CancellationToken.None);
        await MockAccountSession.Received(1).TransitionAsync(UserId, CancellationToken.None);
        MockOfflineSession.Received(1).Activate(capability);
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
    public async Task ItShouldUnlockPreparedOfflineAccessAfterRestorationTimesOut()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(true);
        var capability = new OfflineCapabilityModel(Subject, UserId, IsDeviceReady: true);
        MockOfflineCapability.GetReadyAsync(CancellationToken.None).Returns(capability);
        MockDeviceUnlock.UnlockAsync(Text["OfflineUnlockReason"], CancellationToken.None)
            .Returns(DeviceUnlockResultEnum.Success);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, call.Arg<CancellationToken>());
                return AuthenticationResultEnum.Success;
            });
        // Act
        var result = await CreateSut(TimeSpan.FromMilliseconds(20)).StartAsync(CancellationToken.None);
        // Assert
        result.Should().Be(StartupResultEnum.OfflineSessionReady);
        await MockAuthentication.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
        await MockAccountSession.Received(1).TransitionAsync(UserId, CancellationToken.None);
        MockOfflineSession.Received(1).Activate(capability);
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
