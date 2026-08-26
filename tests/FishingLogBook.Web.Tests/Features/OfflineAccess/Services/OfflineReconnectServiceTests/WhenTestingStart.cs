using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.OfflineAccess.Enums;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.OfflineAccess.Services.OfflineReconnectServiceTests;

public class WhenTestingStart : BaseOfflineReconnectServiceTest
{
    [Fact]
    public async Task ItShouldInvalidateOwnerVerificationWhenConnectivityIsLost()
    {
        // Arrange
        var ownerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOwner = new TaskCompletionSource<CurrentUserDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        MockCurrentUserClient.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            ownerStarted.SetResult();
            return releaseOwner.Task;
        });
        await Sut.StartAsync(CancellationToken.None);

        // Act
        var attempt = Sut.AttemptAsync(CancellationToken.None);
        await ownerStarted.Task;
        MockNetworkService.ConnectivityChanged += Raise.Event<Action<bool>>(false);
        releaseOwner.SetResult(CurrentUser(OfflineUserId));
        await attempt;

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.Offline);
        OfflineOwnerContext.IsUnlocked.Should().BeTrue();
        await MockCatchSynchroniser.DidNotReceive().SynchronisePendingAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldInvalidateSynchronisationWhenConnectivityIsLost()
    {
        // Arrange
        var syncStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSync = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        MockCatchSynchroniser.SynchronisePendingAsync(OfflineUserId, Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                syncStarted.SetResult();
                await releaseSync.Task;
            });
        await Sut.StartAsync(CancellationToken.None);

        // Act
        var attempt = Sut.AttemptAsync(CancellationToken.None);
        await syncStarted.Task;
        MockNetworkService.ConnectivityChanged += Raise.Event<Action<bool>>(false);
        releaseSync.SetResult();
        await attempt;

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.Offline);
        OfflineOwnerContext.IsUnlocked.Should().BeTrue();
        await MockCatchSynchroniser.Received(1).SynchronisePendingAsync(
            OfflineUserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAllowAnExplicitRetryAfterAStaleAttemptCompletes()
    {
        // Arrange
        var ownerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOwner = new TaskCompletionSource<CurrentUserDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        MockCurrentUserClient.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            ownerStarted.SetResult();
            return releaseOwner.Task;
        });
        await Sut.StartAsync(CancellationToken.None);
        var staleAttempt = Sut.AttemptAsync(CancellationToken.None);
        await ownerStarted.Task;
        MockNetworkService.ConnectivityChanged += Raise.Event<Action<bool>>(false);
        releaseOwner.SetResult(CurrentUser(OfflineUserId));
        await staleAttempt;
        MockCurrentUserClient.GetCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(CurrentUser(OfflineUserId));

        // Act
        await Sut.AttemptAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.Online);
        OfflineOwnerContext.IsUnlocked.Should().BeFalse();
        await MockCatchSynchroniser.Received(1).SynchronisePendingAsync(
            OfflineUserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRemainOfflineWithoutResolvingAuthenticationWhenTheNetworkIsOffline()
    {
        // Arrange
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);

        // Act
        await Sut.StartAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.Offline);
        await MockNetworkService.Received(1).StartMonitoringAsync(Arg.Any<CancellationToken>());
        await MockAuthenticationStateProvider.DidNotReceive().GetAuthenticationStateAsync();
        await MockCatchSynchroniser.DidNotReceive().SynchronisePendingAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldExposeARetryableFailureWhenConnectivityCannotBeChecked()
    {
        // Arrange
        var failure = new InvalidOperationException("network unavailable");
        MockNetworkService.StartMonitoringAsync(Arg.Any<CancellationToken>()).ThrowsAsync(failure);

        // Act
        await Sut.StartAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.RetryableFailure);
        OfflineOwnerContext.IsUnlocked.Should().BeTrue();
        await MockAuthenticationStateProvider.DidNotReceive().GetAuthenticationStateAsync();
        await MockLoggingService.Received(1).LogErrorAsync(
            "offline reconnect connectivity",
            failure,
            CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldAttemptRecoveryWhenTheCurrentConnectivityHintIsOnline()
    {
        // Arrange
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await Sut.StartAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.Online);
        await MockCurrentUserClient.Received(1).GetCurrentAsync(Arg.Any<CancellationToken>());
        await MockCatchSynchroniser.Received(1).SynchronisePendingAsync(
            OfflineUserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTriggerSyncedCacheCleanupOnceReconnectSettlesOnline()
    {
        // Arrange
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await Sut.StartAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.Online);
        await MockCatchSynchroniser.Received(1).CleanupSyncedCacheAsync(
            OfflineUserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotTriggerSyncedCacheCleanupWhenReconnectDoesNotReachOnline()
    {
        // Arrange
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);

        // Act
        await Sut.StartAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.Offline);
        await MockCatchSynchroniser.DidNotReceive().CleanupSyncedCacheAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAttemptAutomaticallyOnlyOnceUntilConnectivityIsLostAgain()
    {
        // Arrange
        var failure = new HttpRequestException("unreachable");
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(true);
        MockCurrentUserClient.GetCurrentAsync(Arg.Any<CancellationToken>()).ThrowsAsync(failure);
        await Sut.StartAsync(CancellationToken.None);

        // Act
        MockNetworkService.ConnectivityChanged += Raise.Event<Action<bool>>(true);
        MockNetworkService.ConnectivityChanged += Raise.Event<Action<bool>>(true);
        await Task.Yield();

        // Assert
        await MockCurrentUserClient.Received(1).GetCurrentAsync(Arg.Any<CancellationToken>());
        Sut.State.Should().Be(OfflineReconnectStateEnum.RetryableFailure);

        // Act
        MockNetworkService.ConnectivityChanged += Raise.Event<Action<bool>>(false);
        MockNetworkService.ConnectivityChanged += Raise.Event<Action<bool>>(true);
        await Task.Yield();

        // Assert
        await MockCurrentUserClient.Received(2).GetCurrentAsync(Arg.Any<CancellationToken>());
    }
}
