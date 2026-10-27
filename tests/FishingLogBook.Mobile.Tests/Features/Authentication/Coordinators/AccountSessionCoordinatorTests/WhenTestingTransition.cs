using AwesomeAssertions;

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
