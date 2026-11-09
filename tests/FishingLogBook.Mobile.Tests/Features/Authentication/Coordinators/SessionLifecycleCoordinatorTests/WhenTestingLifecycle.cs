using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Coordinators.SessionLifecycleCoordinatorTests;

public sealed class WhenTestingLifecycle : BaseSessionLifecycleCoordinatorTest
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ItShouldRejectANonPositiveRecoveryTimeout(int seconds)
    {
        // Arrange
        // Act
        var act = () => new SessionLifecycleCoordinator(
            MockConnectivity,
            MockAuthentication,
            MockOfflineSession,
            MockAccountSession,
            MockTimeProvider,
            TimeSpan.FromSeconds(seconds),
            NullLogger<SessionLifecycleCoordinator>.Instance);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ItShouldRefuseToStartAfterBeingDisposed()
    {
        // Arrange
        Sut.Dispose();

        // Act
        var act = () => Sut.Start();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void ItShouldSubscribeToConnectivityOnlyOnceWhenStartedRepeatedly()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        var states = 0;
        Sut.StateChanged += (_, _) => states++;
        Sut.Start();
        Sut.Start();

        // Act
        MockConnectivity.ConnectivityChanged += Raise.Event();

        // Assert
        states.Should().Be(1);
    }

    [Fact]
    public void ItShouldStopReactingToConnectivityAfterBeingDisposedAndTolerateASecondDispose()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        Sut.Start();
        var states = 0;
        Sut.StateChanged += (_, _) => states++;
        Sut.Dispose();

        // Act
        Sut.Dispose();
        MockConnectivity.ConnectivityChanged += Raise.Event();

        // Assert
        states.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldDoNothingWhenForegroundedAfterBeingDisposed()
    {
        // Arrange
        Sut.Dispose();

        // Act
        await Sut.ForegroundAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(SessionLifecycleStateEnum.Idle);
        await MockAuthentication.DidNotReceive().InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldBeIdleWhenOfflineWithNoLocalSession()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        MockOfflineSession.Current.Returns((OfflineCapabilityModel?)null);

        // Act
        await Sut.ForegroundAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(SessionLifecycleStateEnum.Idle);
    }

    [Fact]
    public async Task ItShouldBeIdleWhenOnlineWithNeitherAnOfflineCapabilityNorASession()
    {
        // Arrange
        MockOfflineSession.Current.Returns((OfflineCapabilityModel?)null);
        MockAuthentication.CurrentSession.Returns((AuthenticationSessionModel?)null);

        // Act
        await Sut.ForegroundAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(SessionLifecycleStateEnum.Idle);
        await MockAuthentication.DidNotReceive().InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRequireSignInWhenTheOfflineAccountLeaseIsNoLongerCurrent()
    {
        // Arrange
        MockAccountSession.IsCurrent(Lease).Returns(false);

        // Act
        await Sut.ForegroundAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(SessionLifecycleStateEnum.SignInRequired);
        await MockAuthentication.DidNotReceive().InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRequireSignInWhenTheOfflineCapabilityBelongsToAnotherUser()
    {
        // Arrange
        MockAccountSession.Capture().Returns(new AccountSessionLeaseModel(Guid.NewGuid(), 1, CancellationToken.None));

        // Act
        await Sut.ForegroundAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(SessionLifecycleStateEnum.SignInRequired);
        await MockAuthentication.DidNotReceive().InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRequireSignInWhenTheCaptureReturnsNoLease()
    {
        // Arrange
        MockAccountSession.Capture().Returns((AccountSessionLeaseModel?)null);

        // Act
        await Sut.ForegroundAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(SessionLifecycleStateEnum.SignInRequired);
    }

    [Fact]
    public async Task ItShouldRequireSignInWhenARefreshIsPendingForAnExpiredOnlineSession()
    {
        // Arrange
        MockOfflineSession.Current.Returns((OfflineCapabilityModel?)null);
        MockAuthentication.CurrentSession.Returns(OnlineSession(Now.AddSeconds(30)));
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>()).Returns(AuthenticationResultEnum.Pending);

        // Act
        await Sut.ForegroundAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(SessionLifecycleStateEnum.SignInRequired);
    }

    [Fact]
    public async Task ItShouldBeRecoverableWhenRefreshingAnExpiringOnlineSessionIsTemporarilyUnavailable()
    {
        // Arrange
        MockOfflineSession.Current.Returns((OfflineCapabilityModel?)null);
        MockAuthentication.CurrentSession.Returns(OnlineSession(Now.AddSeconds(30)));
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns(AuthenticationResultEnum.TemporarilyUnavailable);

        // Act
        await Sut.ForegroundAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(SessionLifecycleStateEnum.RecoverableError);
    }

    [Fact]
    public async Task ItShouldStayOnlineAfterRefreshingAnExpiringOnlineSession()
    {
        // Arrange
        MockOfflineSession.Current.Returns((OfflineCapabilityModel?)null);
        MockAuthentication.CurrentSession.Returns(OnlineSession(Now.AddSeconds(30)));
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>()).Returns(AuthenticationResultEnum.Success);

        // Act
        await Sut.ForegroundAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(SessionLifecycleStateEnum.Online);
        await MockAuthentication.Received(1).InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRequireSignInWhenTheOnlineAccountLeaseIsNoLongerCurrentBeforeRefreshing()
    {
        // Arrange
        MockOfflineSession.Current.Returns((OfflineCapabilityModel?)null);
        MockAuthentication.CurrentSession.Returns(OnlineSession(Now.AddSeconds(30)));
        MockAccountSession.IsCurrent(Lease).Returns(false);

        // Act
        await Sut.ForegroundAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(SessionLifecycleStateEnum.SignInRequired);
        await MockAuthentication.DidNotReceive().InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldBeRecoverableAndLogWhenRecoveryThrows()
    {
        // Arrange
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AuthenticationResultEnum>(new InvalidOperationException("boom")));

        // Act
        await Sut.ForegroundAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(SessionLifecycleStateEnum.RecoverableError);
    }

    [Fact]
    public async Task ItShouldTimeOutAHangingRecoveryAsRecoverable()
    {
        // Arrange
        var sut = new SessionLifecycleCoordinator(
            MockConnectivity,
            MockAuthentication,
            MockOfflineSession,
            MockAccountSession,
            MockTimeProvider,
            TimeSpan.FromMilliseconds(100),
            NullLogger<SessionLifecycleCoordinator>.Instance);
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns(call => Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>())
                .ContinueWith(_ => AuthenticationResultEnum.Success, TaskContinuationOptions.OnlyOnRanToCompletion));

        // Act
        await sut.ForegroundAsync(CancellationToken.None);

        // Assert
        sut.State.Should().Be(SessionLifecycleStateEnum.RecoverableError);
    }

    [Fact]
    public async Task ItShouldRunRecoveryAgainWhenRetried()
    {
        // Arrange
        MockAuthentication.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns(AuthenticationResultEnum.TemporarilyUnavailable);
        await Sut.ForegroundAsync(CancellationToken.None);

        // Act
        await Sut.RetryAsync(CancellationToken.None);

        // Assert
        await MockAuthentication.Received(2).InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDropToIdleWhenConnectivityIsLostWithNoLocalSession()
    {
        // Arrange
        MockOfflineSession.Current.Returns((OfflineCapabilityModel?)null);
        MockAuthentication.CurrentSession.Returns(OnlineSession());
        Sut.Start();
        await Sut.ForegroundAsync(CancellationToken.None);
        MockOfflineSession.Current.Returns((OfflineCapabilityModel?)null);
        MockAuthentication.CurrentSession.Returns((AuthenticationSessionModel?)null);
        MockConnectivity.IsOnline.Returns(false);

        // Act
        MockConnectivity.ConnectivityChanged += Raise.Event();

        // Assert
        Sut.State.Should().Be(SessionLifecycleStateEnum.Idle);
    }

    [Fact]
    public async Task ItShouldNotifyListenersOnlyWhenTheStateActuallyChanges()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        var states = 0;
        Sut.StateChanged += (_, _) => states++;

        // Act
        await Sut.ForegroundAsync(CancellationToken.None);
        await Sut.ForegroundAsync(CancellationToken.None);

        // Assert
        states.Should().Be(1);
    }
}
