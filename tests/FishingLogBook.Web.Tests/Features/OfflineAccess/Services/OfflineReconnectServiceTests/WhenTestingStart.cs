using AwesomeAssertions;
using FishingLogBook.Web.Features.OfflineAccess.Enums;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.OfflineAccess.Services.OfflineReconnectServiceTests;

public class WhenTestingStart : BaseOfflineReconnectServiceTest
{
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
