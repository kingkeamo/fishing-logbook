using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Models;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Coordinators.AccountSessionCoordinatorTests;

public class WhenTestingTransition : BaseAccountSessionCoordinatorTest
{
    [Fact]
    public async Task ItShouldRejectAStaleCommitAfterOwnerSwitch()
    {
        // Arrange
        var firstOwner = Guid.NewGuid();
        await Sut.TransitionAsync(firstOwner, CancellationToken.None);
        var staleLease = Sut.Capture()!;
        await Sut.TransitionAsync(Guid.NewGuid(), CancellationToken.None);
        var committed = false;
        // Act
        var accepted = await Sut.TryCommitAsync(
            staleLease,
            _ =>
            {
                committed = true;
                return Task.CompletedTask;
            },
            CancellationToken.None);
        // Assert
        accepted.Should().BeFalse();
        committed.Should().BeFalse();
        staleLease.CancellationToken.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldNotLetAnEarlierLoginOverwriteALaterLogout()
    {
        // Arrange
        await Sut.TransitionAsync(Guid.NewGuid(), CancellationToken.None);
        var originalLease = Sut.Capture()!;
        var releaseCommit = await HoldCommitAsync(originalLease);
        // Act
        var login = Sut.TransitionAsync(Guid.NewGuid(), CancellationToken.None);
        var logout = Sut.TransitionAsync(null, CancellationToken.None);
        releaseCommit.SetResult();
        await Task.WhenAll(login, logout);
        // Assert
        Sut.Capture().Should().BeNull();
        Sut.IsCurrent(originalLease).Should().BeFalse();
        originalLease.CancellationToken.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldNotLetAnEarlierLogoutOverwriteALaterLogin()
    {
        // Arrange
        await Sut.TransitionAsync(Guid.NewGuid(), CancellationToken.None);
        var originalLease = Sut.Capture()!;
        var releaseCommit = await HoldCommitAsync(originalLease);
        var finalOwner = Guid.NewGuid();
        // Act
        var logout = Sut.TransitionAsync(null, CancellationToken.None);
        var login = Sut.TransitionAsync(finalOwner, CancellationToken.None);
        releaseCommit.SetResult();
        await Task.WhenAll(logout, login);
        // Assert
        var finalLease = Sut.Capture()!;
        finalLease.UserId.Should().Be(finalOwner);
        finalLease.Generation.Should().Be(originalLease.Generation + 1);
        Sut.IsCurrent(finalLease).Should().BeTrue();
        Sut.IsCurrent(originalLease).Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldTreatARefreshDuringAPendingSwitchAsTheLatestOwnerRequest()
    {
        // Arrange
        var originalOwner = Guid.NewGuid();
        await Sut.TransitionAsync(originalOwner, CancellationToken.None);
        var originalLease = Sut.Capture()!;
        var releaseCommit = await HoldCommitAsync(originalLease);
        // Act
        var accountSwitch = Sut.TransitionAsync(Guid.NewGuid(), CancellationToken.None);
        var refresh = Sut.TransitionAsync(originalOwner, CancellationToken.None);
        releaseCommit.SetResult();
        await Task.WhenAll(accountSwitch, refresh);
        // Assert
        var finalLease = Sut.Capture()!;
        finalLease.UserId.Should().Be(originalOwner);
        finalLease.Generation.Should().BeGreaterThan(originalLease.Generation);
        Sut.IsCurrent(originalLease).Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldRejectEveryLeaseFromOverlappingTransitionsOnceTheFinalOwnerIsSet()
    {
        // Arrange
        var owners = new Guid?[] { Guid.NewGuid(), Guid.NewGuid(), null };
        var capturedLeases = new System.Collections.Concurrent.ConcurrentBag<AccountSessionLeaseModel>();
        var finalOwner = Guid.NewGuid();
        // Act
        await Task.WhenAll(Enumerable.Range(0, 200).Select(index => Task.Run(async () =>
        {
            await Sut.TransitionAsync(owners[index % owners.Length], CancellationToken.None);
            if (Sut.Capture() is { } lease)
            {
                capturedLeases.Add(lease);
            }
        })));
        await Sut.TransitionAsync(finalOwner, CancellationToken.None);
        var finalLease = Sut.Capture()!;
        var staleCommits = await Task.WhenAll(capturedLeases.Select(lease => Sut.TryCommitAsync(
            lease,
            _ => Task.CompletedTask,
            CancellationToken.None)));
        // Assert
        finalLease.UserId.Should().Be(finalOwner);
        Sut.IsCurrent(finalLease).Should().BeTrue();
        capturedLeases.Should().AllSatisfy(lease =>
            lease.Generation.Should().BeLessThan(finalLease.Generation));
        staleCommits.Should().AllSatisfy(accepted => accepted.Should().BeFalse());
    }

    [Fact]
    public async Task ItShouldInvalidateTheSameOwnersWorkAfterSignOutAndSignIn()
    {
        // Arrange
        var owner = Guid.NewGuid();
        await Sut.TransitionAsync(owner, CancellationToken.None);
        var lease = Sut.Capture()!;
        // Act
        await Sut.TransitionAsync(null, CancellationToken.None);
        await Sut.TransitionAsync(owner, CancellationToken.None);
        // Assert
        Sut.IsCurrent(lease).Should().BeFalse();
        lease.CancellationToken.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldKeepInFlightWorkValidWhenTheSameOwnerRefreshes()
    {
        // Arrange
        var owner = Guid.NewGuid();
        await Sut.TransitionAsync(owner, CancellationToken.None);
        var lease = Sut.Capture()!;
        // Act
        await Sut.TransitionAsync(owner, CancellationToken.None);
        // Assert
        Sut.IsCurrent(lease).Should().BeTrue();
        lease.CancellationToken.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldWaitForAnInFlightCommitBeforeCompletingOwnerSwitch()
    {
        // Arrange
        await Sut.TransitionAsync(Guid.NewGuid(), CancellationToken.None);
        var lease = Sut.Capture()!;
        var commitStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCommit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Act
        var commit = Sut.TryCommitAsync(
            lease,
            async _ =>
            {
                commitStarted.SetResult();
                await releaseCommit.Task;
            },
            CancellationToken.None);
        await commitStarted.Task;
        var transition = Sut.TransitionAsync(Guid.NewGuid(), CancellationToken.None);
        await Task.Yield();
        // Assert
        transition.IsCompleted.Should().BeFalse();
        lease.CancellationToken.IsCancellationRequested.Should().BeTrue();
        releaseCommit.SetResult();
        (await commit).Should().BeFalse();
        await transition;
        Sut.IsCurrent(lease).Should().BeFalse();
    }
}
