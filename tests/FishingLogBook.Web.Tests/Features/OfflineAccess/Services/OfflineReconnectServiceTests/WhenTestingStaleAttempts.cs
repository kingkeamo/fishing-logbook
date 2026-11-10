using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.OfflineAccess.Enums;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.OfflineAccess.Services.OfflineReconnectServiceTests;

public class WhenTestingStaleAttempts : BaseOfflineReconnectServiceTest
{
    [Fact]
    public async Task ItShouldNotVerifyTheOwnerWhenStoppedWhileAuthenticationIsBeingRead()
    {
        // Arrange
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<Microsoft.AspNetCore.Components.Authorization.AuthenticationState>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        MockAuthenticationStateProvider.GetAuthenticationStateAsync().Returns(_ =>
        {
            started.SetResult();
            return release.Task;
        });
        var attempt = Sut.AttemptAsync(CancellationToken.None);
        await started.Task;

        // Act
        Sut.Stop();
        release.SetResult(Authenticated());
        await attempt;

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.Offline);
        await MockCurrentUserClient.DidNotReceive().GetCurrentAsync(Arg.Any<CancellationToken>());
        await MockLoggingService.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreAnAuthenticationReadFailureThatArrivesAfterStop()
    {
        // Arrange
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<Microsoft.AspNetCore.Components.Authorization.AuthenticationState>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        MockAuthenticationStateProvider.GetAuthenticationStateAsync().Returns(_ =>
        {
            started.SetResult();
            return release.Task;
        });
        var attempt = Sut.AttemptAsync(CancellationToken.None);
        await started.Task;

        // Act
        Sut.Stop();
        release.SetException(new InvalidOperationException("late"));
        await attempt;

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.Offline);
        await MockLoggingService.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreAnOwnerVerificationFailureThatArrivesAfterStop()
    {
        // Arrange
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<CurrentUserDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        MockCurrentUserClient.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            started.SetResult();
            return release.Task;
        });
        var attempt = Sut.AttemptAsync(CancellationToken.None);
        await started.Task;

        // Act
        Sut.Stop();
        release.SetException(new InvalidOperationException("late"));
        await attempt;

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.Offline);
        await MockLoggingService.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
        await MockLogbookSynchroniser.DidNotReceive().SynchronisePendingAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRequireAuthenticationWhenAnUnauthorizedOwnerLookupArrivesAfterStop()
    {
        // Arrange
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<CurrentUserDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        MockCurrentUserClient.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            started.SetResult();
            return release.Task;
        });
        var attempt = Sut.AttemptAsync(CancellationToken.None);
        await started.Task;

        // Act
        Sut.Stop();
        release.SetException(new HttpRequestException("unauthorized", null, System.Net.HttpStatusCode.Unauthorized));
        await attempt;

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.Offline);
    }

    [Fact]
    public async Task ItShouldIgnoreASynchronisationFailureThatArrivesAfterStop()
    {
        // Arrange
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MockLogbookSynchroniser.SynchronisePendingAsync(OfflineUserId, Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                started.SetResult();
                await release.Task;
            });
        var attempt = Sut.AttemptAsync(CancellationToken.None);
        await started.Task;

        // Act
        Sut.Stop();
        release.SetException(new InvalidOperationException("late"));
        await attempt;

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.Offline);
        OfflineOwnerContext.IsUnlocked.Should().BeTrue();
        await MockLoggingService.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStayOfflineWhenSynchronisationIsCancelled()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        MockLogbookSynchroniser.SynchronisePendingAsync(OfflineUserId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellation.Cancel();
                return Task.FromCanceled(cancellation.Token);
            });

        // Act
        await Sut.AttemptAsync(cancellation.Token);

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.Synchronising);
        OfflineOwnerContext.IsUnlocked.Should().BeTrue();
        await MockLoggingService.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopQuietlyWhenOwnerVerificationIsCancelled()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        MockCurrentUserClient.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            cancellation.Cancel();
            return Task.FromCanceled<CurrentUserDto>(cancellation.Token);
        });

        // Act
        await Sut.AttemptAsync(cancellation.Token);

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.VerifyingOwner);
        await MockLoggingService.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopQuietlyWhenReadingAuthenticationIsCancelled()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var release = new TaskCompletionSource<Microsoft.AspNetCore.Components.Authorization.AuthenticationState>();
        MockAuthenticationStateProvider.GetAuthenticationStateAsync().Returns(release.Task);
        var attempt = Sut.AttemptAsync(cancellation.Token);

        // Act
        await cancellation.CancelAsync();
        await attempt;

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.RecoveringAuthentication);
        await MockCurrentUserClient.DidNotReceive().GetCurrentAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ItShouldRemainOfflineWhenStoppedWithoutEverStarting()
    {
        // Arrange
        // Act
        Sut.Stop();
        Sut.Stop();

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.Offline);
    }

    [Fact]
    public async Task ItShouldKeepASingleConnectivitySubscriptionWhenStartedTwice()
    {
        // Arrange
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);

        // Act
        await Sut.StartAsync(CancellationToken.None);
        await Sut.StartAsync(CancellationToken.None);

        // Assert
        await MockNetworkService.Received(2).StartMonitoringAsync(Arg.Any<CancellationToken>());
        await MockNetworkService.Received(2).IsOnlineAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnQuietlyWhenStartingWithACancelledToken()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        MockNetworkService.StartMonitoringAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));

        // Act
        await Sut.StartAsync(cancellation.Token);

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.Offline);
        await MockLoggingService.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }
}
