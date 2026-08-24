using System.Net;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.OfflineAccess.Enums;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.OfflineAccess.Services.OfflineReconnectServiceTests;

public class WhenTestingAttempt : BaseOfflineReconnectServiceTest
{
    [Fact]
    public async Task ItShouldRequireAuthenticationWithoutResolvingOrSynchronisingAnOwner()
    {
        // Arrange
        MockAuthenticationStateProvider.GetAuthenticationStateAsync().Returns(Anonymous());

        // Act
        await Sut.AttemptAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.AuthenticationRequired);
        OfflineOwnerContext.IsUnlocked.Should().BeTrue();
        await MockCurrentUserClient.DidNotReceive().GetCurrentAsync(Arg.Any<CancellationToken>());
        await MockCatchSynchroniser.DidNotReceive().SynchronisePendingAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRequireAuthenticationWhenTheTrustedRequestIsUnauthorized()
    {
        // Arrange
        MockCurrentUserClient.GetCurrentAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("unauthorized", null, HttpStatusCode.Unauthorized));

        // Act
        await Sut.AttemptAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.AuthenticationRequired);
        OfflineOwnerContext.IsUnlocked.Should().BeTrue();
        await MockCatchSynchroniser.DidNotReceive().SynchronisePendingAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await MockLoggingService.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailClosedWhenTheAuthenticatedOwnerDoesNotMatch()
    {
        // Arrange
        MockCurrentUserClient.GetCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(CurrentUser(OtherUserId));

        // Act
        await Sut.AttemptAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.OwnerMismatch);
        OfflineOwnerContext.Owner!.UserId.Should().Be(OfflineUserId);
        await MockCurrentUserClient.Received(1).GetCurrentAsync(Arg.Any<CancellationToken>());
        await MockCatchSynchroniser.DidNotReceive().SynchronisePendingAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPreserveTheOfflineOwnerWhenTrustedOwnerResolutionFails()
    {
        // Arrange
        var failure = new HttpRequestException("unreachable");
        MockCurrentUserClient.GetCurrentAsync(Arg.Any<CancellationToken>()).ThrowsAsync(failure);

        // Act
        await Sut.AttemptAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.RetryableFailure);
        OfflineOwnerContext.IsUnlocked.Should().BeTrue();
        await MockCatchSynchroniser.DidNotReceive().SynchronisePendingAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await MockLoggingService.Received(1).LogErrorAsync(
            "offline reconnect owner verification",
            failure,
            CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldPreserveTheOfflineOwnerWhenSynchronisationFails()
    {
        // Arrange
        var failure = new InvalidOperationException("sync failed");
        MockCatchSynchroniser.SynchronisePendingAsync(OfflineUserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(failure);

        // Act
        await Sut.AttemptAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.RetryableFailure);
        OfflineOwnerContext.IsUnlocked.Should().BeTrue();
        await MockCatchSynchroniser.Received(1).SynchronisePendingAsync(
            OfflineUserId,
            Arg.Any<CancellationToken>());
        await MockLoggingService.Received(1).LogErrorAsync(
            "offline reconnect synchronisation",
            failure,
            CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldNotSynchroniseWhenTheOfflineContextIsLockedDuringOwnerVerification()
    {
        // Arrange
        var ownerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOwner = new TaskCompletionSource<CurrentUserDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        MockCurrentUserClient.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            ownerStarted.SetResult();
            return releaseOwner.Task;
        });

        // Act
        var attempt = Sut.AttemptAsync(CancellationToken.None);
        await ownerStarted.Task;
        OfflineOwnerContext.Lock();
        releaseOwner.SetResult(CurrentUser(OfflineUserId));
        await attempt;

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.Offline);
        await MockCatchSynchroniser.DidNotReceive().SynchronisePendingAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPreventDuplicateReconnectAndSynchronisationAttempts()
    {
        // Arrange
        var syncStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSync = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MockCatchSynchroniser.SynchronisePendingAsync(
            OfflineUserId,
            Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            syncStarted.SetResult();
            await releaseSync.Task;
        });

        // Act
        var first = Sut.AttemptAsync(CancellationToken.None);
        await syncStarted.Task;
        var second = Sut.AttemptAsync(CancellationToken.None);
        releaseSync.SetResult();
        await Task.WhenAll(first, second);

        // Assert
        await MockCurrentUserClient.Received(1).GetCurrentAsync(Arg.Any<CancellationToken>());
        await MockCatchSynchroniser.Received(1).SynchronisePendingAsync(
            OfflineUserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLockOnlyAfterTheMatchingOwnerSynchronisationCompletes()
    {
        // Arrange
        var syncStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSync = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MockCatchSynchroniser.SynchronisePendingAsync(
            OfflineUserId,
            Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            syncStarted.SetResult();
            await releaseSync.Task;
        });

        // Act
        var attempt = Sut.AttemptAsync(CancellationToken.None);
        await syncStarted.Task;

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.Synchronising);
        OfflineOwnerContext.IsUnlocked.Should().BeTrue();
        releaseSync.SetResult();
        await attempt;
        Sut.State.Should().Be(OfflineReconnectStateEnum.Online);
        OfflineOwnerContext.IsUnlocked.Should().BeFalse();
        await MockCurrentUserClient.Received(1).GetCurrentAsync(Arg.Any<CancellationToken>());
        await MockCatchSynchroniser.Received(1).SynchronisePendingAsync(
            OfflineUserId,
            Arg.Any<CancellationToken>());
    }
}
