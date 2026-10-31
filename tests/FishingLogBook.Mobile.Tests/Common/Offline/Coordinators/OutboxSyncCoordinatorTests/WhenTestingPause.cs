using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Models;
using FishingLogBook.Mobile.Features.Authentication.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Common.Offline.Coordinators.OutboxSyncCoordinatorTests;

public sealed class WhenTestingPause : BaseOutboxSyncCoordinatorTest
{
    [Fact]
    public void ItShouldNotThrowWhenNothingIsRunning()
    {
        // Arrange
        // Act
        var act = () => Sut.Pause();

        // Assert
        act.Should().NotThrow();
        MockTimer.DidNotReceive().Dispose();
    }

    [Fact]
    public async Task ItShouldDisposeThePendingRetryTimer()
    {
        // Arrange
        await SeedAsync(availableAt: Now.AddMinutes(1));
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Act
        Sut.Pause();

        // Assert
        MockTimer.Received(1).Dispose();
    }

    [Fact]
    public async Task ItShouldCancelTheActiveRunAndLeaveTheOperationPendingWithoutConsumingAnAttempt()
    {
        // Arrange
        var id = await SeedAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RespondWith(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return OutboxOperationResultModel.Completed();
        });
        var run = Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);
        await started.Task;

        // Act
        Sut.Pause();
        await run;

        // Assert
        var row = await ReadRowAsync(id);
        row.Status.Should().Be(OutboxStatusEnum.Pending);
        row.AttemptCount.Should().Be(0);
        row.LeaseOwner.Should().BeNull();
        row.LeaseExpiresAt.Should().BeNull();
        (await Store.ListPendingAsync(UserId, CancellationToken.None)).Should().ContainSingle(o => o.Id == id);
    }

    [Fact]
    public async Task ItShouldAllowALaterRunToProcessTheReleasedOperation()
    {
        // Arrange
        var id = await SeedAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        RespondWith(async (_, token) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token);
            }

            return OutboxOperationResultModel.Completed();
        });
        var run = Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);
        await started.Task;
        Sut.Pause();
        await run;

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.SessionOnline, CancellationToken.None);

        // Assert
        var row = await ReadRowAsync(id);
        row.Status.Should().Be(OutboxStatusEnum.Completed);
        row.AttemptCount.Should().Be(1);
        await MockSynchroniser.Received(2).SynchroniseAsync(
            Arg.Is<OutboxOperationModel>(operation => operation.Id == id),
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<CancellationToken>());
    }
}
