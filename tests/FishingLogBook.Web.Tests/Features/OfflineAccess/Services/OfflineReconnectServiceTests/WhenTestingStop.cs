using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.OfflineAccess.Enums;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.OfflineAccess.Services.OfflineReconnectServiceTests;

public class WhenTestingStop : BaseOfflineReconnectServiceTest
{
    [Fact]
    public async Task ItShouldPreventOwnerVerificationFromStartingLateSynchronisationAfterStop()
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
        var attempt = Sut.AttemptAsync(CancellationToken.None);
        await ownerStarted.Task;

        // Act
        Sut.Stop();
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
    public async Task ItShouldPreventSynchronisationFromLockingTheOwnerAfterStop()
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
        var attempt = Sut.AttemptAsync(CancellationToken.None);
        await syncStarted.Task;

        // Act
        Sut.Stop();
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
    public async Task ItShouldCancelAnInFlightConnectivityRecoveryBeforeLockingTheOwner()
    {
        // Arrange
        var ownerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        MockCurrentUserClient.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(async call =>
        {
            ownerStarted.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, call.Arg<CancellationToken>());
            return CurrentUser(OfflineUserId);
        });
        await Sut.StartAsync(CancellationToken.None);

        // Act
        MockNetworkService.ConnectivityChanged += Raise.Event<Action<bool>>(true);
        await ownerStarted.Task;
        Sut.Stop();
        await Task.Yield();

        // Assert
        OfflineOwnerContext.IsUnlocked.Should().BeTrue();
        await MockCatchSynchroniser.DidNotReceive().SynchronisePendingAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopRespondingToConnectivityChanges()
    {
        // Arrange
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        await Sut.StartAsync(CancellationToken.None);

        // Act
        Sut.Stop();
        MockNetworkService.ConnectivityChanged += Raise.Event<Action<bool>>(true);
        await Task.Yield();

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.Offline);
        await MockAuthenticationStateProvider.DidNotReceive().GetAuthenticationStateAsync();
        await MockCatchSynchroniser.DidNotReceive().SynchronisePendingAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }
}
