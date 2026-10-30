using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Coordinators.SessionLifecycleCoordinatorTests;

public sealed class WhenTestingForeground : BaseSessionLifecycleCoordinatorTest
{
    [Fact]
    public async Task ItShouldKeepTheLocalSessionWithoutCallingAuthenticationWhileOffline()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);

        // Act
        await Sut.ForegroundAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(SessionLifecycleStateEnum.LocalOffline);
        await MockAuthentication.DidNotReceive().InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldExposeARecoverableStateWithoutCreatingAnOnlineSessionAfterATemporaryFailure()
    {
        // Arrange
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns(AuthenticationResultEnum.TemporarilyUnavailable);

        // Act
        await Sut.ForegroundAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(SessionLifecycleStateEnum.RecoverableError);
        MockAuthentication.CurrentSession.Should().BeNull();
        MockOfflineSession.Current.Should().Be(Capability);
    }

    [Fact]
    public async Task ItShouldRequireSignInAfterPermanentAuthenticationRejection()
    {
        // Arrange
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns(AuthenticationResultEnum.Invalid);

        // Act
        await Sut.ForegroundAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(SessionLifecycleStateEnum.SignInRequired);
    }

    [Fact]
    public async Task ItShouldActivateOnlineOnlyAfterTheOfflineOwnerIsVerified()
    {
        // Arrange
        AuthenticationSessionModel? session = null;
        OfflineCapabilityModel? offline = Capability;
        MockAuthentication.CurrentSession.Returns(_ => session);
        MockOfflineSession.Current.Returns(_ => offline);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            session = OnlineSession();
            offline = null;
            return AuthenticationResultEnum.Success;
        });

        // Act
        await Sut.ForegroundAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(SessionLifecycleStateEnum.Online);
        session.Should().NotBeNull();
        session!.Subject.Should().Be(Subject);
        session.UserId.Should().Be(UserId);
    }

    [Fact]
    public async Task ItShouldNotActivateOnlineForADifferentAccount()
    {
        // Arrange
        var session = OnlineSession() with { Subject = "different-subject", UserId = Guid.NewGuid() };
        MockAuthentication.CurrentSession.Returns(session);
        MockOfflineSession.Current.Returns(Capability, (FishingLogBook.Mobile.Features.OfflineAccess.Models.OfflineCapabilityModel?)null);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns(AuthenticationResultEnum.Success);

        // Act
        await Sut.ForegroundAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(SessionLifecycleStateEnum.RecoverableError);
    }

    [Fact]
    public async Task ItShouldNotActivateALateSuccessAfterTheAccountLeaseChanges()
    {
        // Arrange
        var recovery = new TaskCompletionSource<AuthenticationResultEnum>(TaskCreationOptions.RunContinuationsAsynchronously);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>()).Returns(recovery.Task);
        var operation = Sut.ForegroundAsync(CancellationToken.None);
        MockAuthentication.CurrentSession.Returns(OnlineSession());
        MockOfflineSession.Current.Returns((FishingLogBook.Mobile.Features.OfflineAccess.Models.OfflineCapabilityModel?)null);
        MockAccountSession.IsCurrent(Lease).Returns(false);

        // Act
        recovery.SetResult(AuthenticationResultEnum.Success);
        await operation;

        // Assert
        Sut.State.Should().Be(SessionLifecycleStateEnum.RecoverableError);
    }

    [Fact]
    public async Task ItShouldShareOneRecoveryAcrossSimultaneousConnectivityAndForegroundEvents()
    {
        // Arrange
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recovery = new TaskCompletionSource<AuthenticationResultEnum>(TaskCreationOptions.RunContinuationsAsynchronously);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            started.SetResult();
            return recovery.Task;
        });
        Sut.Start();

        // Act
        MockConnectivity.ConnectivityChanged += Raise.Event();
        await started.Task;
        var foreground = Sut.ForegroundAsync(CancellationToken.None);
        recovery.SetResult(AuthenticationResultEnum.TemporarilyUnavailable);
        await foreground;

        // Assert
        await MockAuthentication.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCancelRecoveryWhenTheAppIsBackgrounded()
    {
        // Arrange
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource<AuthenticationResultEnum>(TaskCreationOptions.RunContinuationsAsynchronously);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>()).Returns(async call =>
        {
            started.SetResult();
            using var registration = call.Arg<CancellationToken>().Register(() => cancelled.TrySetCanceled());
            return await cancelled.Task;
        });
        var operation = Sut.ForegroundAsync(CancellationToken.None);
        await started.Task;

        // Act
        Sut.Background();
        await operation;

        // Assert
        Sut.State.Should().Be(SessionLifecycleStateEnum.LocalOffline);
        MockAuthentication.CurrentSession.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldRestartCancelledRecoveryWhenTheAppReturnsToForeground()
    {
        // Arrange
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource<AuthenticationResultEnum>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>()).Returns(async call =>
        {
            calls++;
            if (calls == 1)
            {
                started.SetResult();
                using var registration = call.Arg<CancellationToken>().Register(() => cancelled.TrySetCanceled());
                return await cancelled.Task;
            }

            return AuthenticationResultEnum.TemporarilyUnavailable;
        });
        var first = Sut.ForegroundAsync(CancellationToken.None);
        await started.Task;
        Sut.Background();

        // Act
        var resumed = Sut.ForegroundAsync(CancellationToken.None);
        await Task.WhenAll(first, resumed);

        // Assert
        Sut.State.Should().Be(SessionLifecycleStateEnum.RecoverableError);
        await MockAuthentication.Received(2).InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAvoidRefreshingAValidOnlineSessionOnRepeatedForegroundEvents()
    {
        // Arrange
        MockOfflineSession.Current.Returns((FishingLogBook.Mobile.Features.OfflineAccess.Models.OfflineCapabilityModel?)null);
        MockAuthentication.CurrentSession.Returns(OnlineSession());

        // Act
        await Sut.ForegroundAsync(CancellationToken.None);
        await Sut.ForegroundAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(SessionLifecycleStateEnum.Online);
        await MockAuthentication.DidNotReceive().InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRestoreAnExpiredOnlineSessionOnForeground()
    {
        // Arrange
        var expired = OnlineSession(Now.AddMinutes(-1));
        MockOfflineSession.Current.Returns((FishingLogBook.Mobile.Features.OfflineAccess.Models.OfflineCapabilityModel?)null);
        MockAuthentication.CurrentSession.Returns(expired, OnlineSession());
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>()).Returns(AuthenticationResultEnum.Success);

        // Act
        await Sut.ForegroundAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(SessionLifecycleStateEnum.Online);
        await MockAuthentication.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
    }
}
