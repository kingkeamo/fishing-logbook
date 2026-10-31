using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Constants;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Models;
using FishingLogBook.Mobile.Features.Authentication.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Common.Offline.Coordinators.OutboxSyncCoordinatorTests;

public sealed class WhenTestingRun : BaseOutboxSyncCoordinatorTest
{
    [Fact]
    public async Task ItShouldDoNoWorkWhenThereIsNoSessionLease()
    {
        // Arrange
        MockSession.Capture().Returns((AccountSessionLeaseModel?)null);
        var id = await SeedAsync();

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        await MockSynchroniser.DidNotReceive().SynchroniseAsync(
            Arg.Any<OutboxOperationModel>(),
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<CancellationToken>());
        MockNotifier.DidNotReceive().NotifyStateChanged();
        var row = await ReadRowAsync(id);
        row.Status.Should().Be(OutboxStatusEnum.Pending);
        row.AttemptCount.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldNotClaimAnythingWhenOffline()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        var id = await SeedAsync();

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Foreground, CancellationToken.None);

        // Assert
        await MockSynchroniser.DidNotReceive().SynchroniseAsync(
            Arg.Any<OutboxOperationModel>(),
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<CancellationToken>());
        var row = await ReadRowAsync(id);
        row.Status.Should().Be(OutboxStatusEnum.Pending);
        row.AttemptCount.Should().Be(0);
        row.LeaseOwner.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldNotClaimAnOperationScheduledInTheFutureAndShouldScheduleATimerForIt()
    {
        // Arrange
        var future = await SeedAsync(availableAt: Now.AddMinutes(1));

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        await MockSynchroniser.DidNotReceive().SynchroniseAsync(
            Arg.Any<OutboxOperationModel>(),
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<CancellationToken>());
        var row = await ReadRowAsync(future);
        row.Status.Should().Be(OutboxStatusEnum.Pending);
        row.AttemptCount.Should().Be(0);
        MockTime.Received(1).CreateTimer(
            Arg.Any<TimerCallback>(),
            Arg.Any<object?>(),
            Arg.Is<TimeSpan>(due => due == TimeSpan.FromMinutes(1)),
            Arg.Is<TimeSpan>(period => period == Timeout.InfiniteTimeSpan));
    }

    [Fact]
    public async Task ItShouldUseTheMinimumTimerDelayWhenTheNextOperationIsAlreadyDue()
    {
        // Arrange
        await SeedAsync();
        RespondWith(_ => Task.FromResult(OutboxOperationResultModel.Paused()));

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        MockTime.Received(1).CreateTimer(
            Arg.Any<TimerCallback>(),
            Arg.Any<object?>(),
            Arg.Is<TimeSpan>(due => due == TimeSpan.FromSeconds(1)),
            Arg.Is<TimeSpan>(period => period == Timeout.InfiniteTimeSpan));
    }

    [Fact]
    public async Task ItShouldNotScheduleATimerWhenNothingIsPending()
    {
        // Arrange
        await SeedAsync();

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        MockTime.DidNotReceive().CreateTimer(
            Arg.Any<TimerCallback>(),
            Arg.Any<object?>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task ItShouldKeepTheRetryDelayAndFailureStateWhenTheSynchroniserRequestsARetry()
    {
        // Arrange
        var id = await SeedAsync();
        RespondWith(_ => Task.FromResult(OutboxOperationResultModel.Retry("ServerDown")));

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        var row = await ReadRowAsync(id);
        row.Status.Should().Be(OutboxStatusEnum.Pending);
        row.AttemptCount.Should().Be(1);
        row.AvailableAt.Should().Be(Now + RetryDelay);
        row.LastError.Should().Be("ServerDown");
        row.LeaseOwner.Should().BeNull();
        row.LeaseExpiresAt.Should().BeNull();
        MockTime.Received(1).CreateTimer(
            Arg.Any<TimerCallback>(),
            Arg.Any<object?>(),
            Arg.Is<TimeSpan>(due => due == RetryDelay),
            Arg.Is<TimeSpan>(period => period == Timeout.InfiniteTimeSpan));
    }

    [Fact]
    public async Task ItShouldDoubleTheRetryDelayOnTheSecondAttempt()
    {
        // Arrange
        var id = await SeedAsync(attemptCount: 1);
        RespondWith(_ => Task.FromResult(OutboxOperationResultModel.Retry("ServerDown")));

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        var row = await ReadRowAsync(id);
        row.Status.Should().Be(OutboxStatusEnum.Pending);
        row.AttemptCount.Should().Be(2);
        row.AvailableAt.Should().Be(Now + (RetryDelay * 2));
    }

    [Fact]
    public async Task ItShouldMarkTheOperationDeadWhenTheLastAllowedAttemptStillRequestsARetry()
    {
        // Arrange
        var id = await SeedAsync(attemptCount: MaximumAttempts - 1);
        RespondWith(_ => Task.FromResult(OutboxOperationResultModel.Retry("ServerDown")));

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        var row = await ReadRowAsync(id);
        row.Status.Should().Be(OutboxStatusEnum.Dead);
        row.AttemptCount.Should().Be(MaximumAttempts);
        row.LastError.Should().Be("ServerDown");
        row.LeaseOwner.Should().BeNull();
        MockTime.DidNotReceive().CreateTimer(
            Arg.Any<TimerCallback>(),
            Arg.Any<object?>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task ItShouldStillRetryWhenOneAttemptRemains()
    {
        // Arrange
        var id = await SeedAsync(attemptCount: MaximumAttempts - 2);
        RespondWith(_ => Task.FromResult(OutboxOperationResultModel.Retry("ServerDown")));

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        var row = await ReadRowAsync(id);
        row.Status.Should().Be(OutboxStatusEnum.Pending);
        row.AttemptCount.Should().Be(MaximumAttempts - 1);
    }

    [Fact]
    public async Task ItShouldReleaseWithoutConsumingAnAttemptAndStopWhenConnectivityDropsDuringARetry()
    {
        // Arrange
        var first = await SeedAsync();
        var second = await SeedAsync();
        RespondWith(_ =>
        {
            MockConnectivity.IsOnline.Returns(false);
            return Task.FromResult(OutboxOperationResultModel.Retry("Network"));
        });

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        await MockSynchroniser.Received(1).SynchroniseAsync(
            Arg.Any<OutboxOperationModel>(),
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<CancellationToken>());
        var firstRow = await ReadRowAsync(first);
        firstRow.Status.Should().Be(OutboxStatusEnum.Pending);
        firstRow.AttemptCount.Should().Be(0);
        firstRow.AvailableAt.Should().Be(Now);
        firstRow.LastError.Should().BeNull();
        var secondRow = await ReadRowAsync(second);
        secondRow.Status.Should().Be(OutboxStatusEnum.Pending);
        secondRow.AttemptCount.Should().Be(0);
        MockTime.DidNotReceive().CreateTimer(
            Arg.Any<TimerCallback>(),
            Arg.Any<object?>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task ItShouldMarkTheOperationFailedAndNeverRetryItWhenTheResultIsTerminal()
    {
        // Arrange
        var id = await SeedAsync();
        RespondWith(_ => Task.FromResult(OutboxOperationResultModel.Terminal("BadRequest")));

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);
        await Sut.RunAsync(OutboxSyncTriggerEnum.RetryDue, CancellationToken.None);

        // Assert
        await MockSynchroniser.Received(1).SynchroniseAsync(
            Arg.Is<OutboxOperationModel>(operation => operation.Id == id),
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<CancellationToken>());
        var row = await ReadRowAsync(id);
        row.Status.Should().Be(OutboxStatusEnum.Failed);
        row.AttemptCount.Should().Be(1);
        row.LastError.Should().Be("BadRequest");
        row.LeaseOwner.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldReleaseWithoutConsumingAnAttemptAndStopTheRunWhenTheSynchroniserIsPaused()
    {
        // Arrange
        var first = await SeedAsync();
        var second = await SeedAsync();
        RespondWith(_ => Task.FromResult(OutboxOperationResultModel.Paused()));

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        await MockSynchroniser.Received(1).SynchroniseAsync(
            Arg.Any<OutboxOperationModel>(),
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<CancellationToken>());
        var firstRow = await ReadRowAsync(first);
        firstRow.Status.Should().Be(OutboxStatusEnum.Pending);
        firstRow.AttemptCount.Should().Be(0);
        firstRow.LeaseOwner.Should().BeNull();
        var secondRow = await ReadRowAsync(second);
        secondRow.Status.Should().Be(OutboxStatusEnum.Pending);
        secondRow.AttemptCount.Should().Be(0);
        var pending = await Store.ListPendingAsync(UserId, CancellationToken.None);
        pending.Select(operation => operation.Id).Should().BeEquivalentTo([first, second]);
    }

    [Fact]
    public async Task ItShouldRetryWithAnUnexpectedErrorAndContinueWithTheNextOperationWhenTheSynchroniserThrows()
    {
        // Arrange
        var failing = await SeedAsync();
        var succeeding = await SeedAsync();
        RespondWith(operation => operation.Id == failing
            ? throw new InvalidOperationException("boom")
            : Task.FromResult(OutboxOperationResultModel.Completed()));

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        await MockSynchroniser.Received(2).SynchroniseAsync(
            Arg.Any<OutboxOperationModel>(),
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<CancellationToken>());
        var failedRow = await ReadRowAsync(failing);
        failedRow.Status.Should().Be(OutboxStatusEnum.Pending);
        failedRow.AttemptCount.Should().Be(1);
        failedRow.LastError.Should().Be(OutboxErrorCodeConstants.Unexpected);
        failedRow.AvailableAt.Should().Be(Now + RetryDelay);
        var succeededRow = await ReadRowAsync(succeeding);
        succeededRow.Status.Should().Be(OutboxStatusEnum.Completed);
    }

    [Fact]
    public async Task ItShouldReleaseTheOperationAndEndTheRunWithoutThrowingWhenCancelledDuringSynchronisation()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var first = await SeedAsync();
        var second = await SeedAsync();
        RespondWith(async (_, token) =>
        {
            await cancellation.CancelAsync();
            token.ThrowIfCancellationRequested();
            return OutboxOperationResultModel.Completed();
        });

        // Act
        var act = () => Sut.RunAsync(OutboxSyncTriggerEnum.Manual, cancellation.Token);

        // Assert
        await act.Should().NotThrowAsync();
        await MockSynchroniser.Received(1).SynchroniseAsync(
            Arg.Any<OutboxOperationModel>(),
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<CancellationToken>());
        var firstRow = await ReadRowAsync(first);
        firstRow.Status.Should().Be(OutboxStatusEnum.Pending);
        firstRow.AttemptCount.Should().Be(0);
        firstRow.LeaseOwner.Should().BeNull();
        var secondRow = await ReadRowAsync(second);
        secondRow.Status.Should().Be(OutboxStatusEnum.Pending);
        secondRow.AttemptCount.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldFailTheOperationWhenNoSynchroniserIsRegisteredForItsType()
    {
        // Arrange
        var unsupported = await SeedAsync(operationType: OutboxOperationTypeEnum.DeleteCatch);
        var supported = await SeedAsync();

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        await MockSynchroniser.Received(1).SynchroniseAsync(
            Arg.Is<OutboxOperationModel>(operation => operation.Id == supported),
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<CancellationToken>());
        await MockSynchroniser.DidNotReceive().SynchroniseAsync(
            Arg.Is<OutboxOperationModel>(operation => operation.Id == unsupported),
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<CancellationToken>());
        var row = await ReadRowAsync(unsupported);
        row.Status.Should().Be(OutboxStatusEnum.Failed);
        row.LastError.Should().Be(OutboxErrorCodeConstants.NoSynchroniser);
        row.AttemptCount.Should().Be(1);
        (await ReadRowAsync(supported)).Status.Should().Be(OutboxStatusEnum.Completed);
    }

    [Fact]
    public async Task ItShouldNeverClaimOrSynchroniseAnotherUsersOperations()
    {
        // Arrange
        var other = await SeedAsync(userId: OtherUserId);
        var own = await SeedAsync();

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        await MockSynchroniser.DidNotReceive().SynchroniseAsync(
            Arg.Is<OutboxOperationModel>(operation => operation.UserId == OtherUserId),
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<CancellationToken>());
        await MockSynchroniser.Received(1).SynchroniseAsync(
            Arg.Is<OutboxOperationModel>(operation => operation.Id == own && operation.UserId == UserId),
            Arg.Is<AccountSessionLeaseModel>(lease => lease.UserId == UserId),
            Arg.Any<CancellationToken>());
        var otherRow = await ReadRowAsync(other);
        otherRow.Status.Should().Be(OutboxStatusEnum.Pending);
        otherRow.AttemptCount.Should().Be(0);
        otherRow.LeaseOwner.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldNotRecoverAnotherUsersInProgressOperations()
    {
        // Arrange
        var other = await SeedAsync(userId: OtherUserId);
        await ExecuteSqlAsync(
            "UPDATE outboxoperations SET status = 'InProgress', leaseowner = 'someone', leaseexpiresat = $expires WHERE id = $id;",
            ("$expires", (Now - TimeSpan.FromMinutes(5)).ToString("O")),
            ("$id", other.ToString("D")));

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        var row = await ReadRowAsync(other);
        row.Status.Should().Be(OutboxStatusEnum.InProgress);
        row.LeaseOwner.Should().Be("someone");
    }

    [Fact]
    public async Task ItShouldStopClaimingWhenTheSessionLeaseIsReplacedDuringTheRun()
    {
        // Arrange
        var first = await SeedAsync();
        var second = await SeedAsync();
        RespondWith(_ =>
        {
            MockSession.IsCurrent(Arg.Any<AccountSessionLeaseModel>()).Returns(false);
            return Task.FromResult(OutboxOperationResultModel.Completed());
        });

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        await MockSynchroniser.Received(1).SynchroniseAsync(
            Arg.Is<OutboxOperationModel>(operation => operation.Id == first),
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<CancellationToken>());
        (await ReadRowAsync(first)).Status.Should().Be(OutboxStatusEnum.Completed);
        var secondRow = await ReadRowAsync(second);
        secondRow.Status.Should().Be(OutboxStatusEnum.Pending);
        secondRow.AttemptCount.Should().Be(0);
        MockTime.DidNotReceive().CreateTimer(
            Arg.Any<TimerCallback>(),
            Arg.Any<object?>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task ItShouldRecoverAnInProgressOperationWithAnExpiredLeaseAndProcessItInTheSameRun()
    {
        // Arrange
        var id = await SeedAsync(attemptCount: 1);
        await ExecuteSqlAsync(
            "UPDATE outboxoperations SET status = 'InProgress', leaseowner = 'previous', leaseexpiresat = $expires WHERE id = $id;",
            ("$expires", (Now - TimeSpan.FromMinutes(1)).ToString("O")),
            ("$id", id.ToString("D")));

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        await MockSynchroniser.Received(1).SynchroniseAsync(
            Arg.Is<OutboxOperationModel>(operation => operation.Id == id && operation.AttemptCount == 2),
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<CancellationToken>());
        var row = await ReadRowAsync(id);
        row.Status.Should().Be(OutboxStatusEnum.Completed);
        row.LeaseOwner.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldImmediatelyRecoverAnInProgressOperationOwnedByAPreviousProcessWithAnUnexpiredLease()
    {
        // Arrange
        var id = await SeedAsync();
        await ExecuteSqlAsync(
            "UPDATE outboxoperations SET status = 'InProgress', leaseowner = 'previous-process', leaseexpiresat = $expires WHERE id = $id;",
            ("$expires", (Now + TimeSpan.FromMinutes(4)).ToString("O")),
            ("$id", id.ToString("D")));

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        await MockSynchroniser.Received(1).SynchroniseAsync(
            Arg.Is<OutboxOperationModel>(operation => operation.Id == id),
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<CancellationToken>());
        (await ReadRowAsync(id)).Status.Should().Be(OutboxStatusEnum.Completed);
    }

    [Fact]
    public async Task ItShouldNotCompleteTheOperationOrKeepPersistedWritesWhenPersistThrowsAnInvalidOperation()
    {
        // Arrange
        var id = await SeedAsync();
        var other = await SeedAsync(availableAt: Now.AddHours(1));
        var persist = PersistPayload(other, "persisted");
        RespondWith(_ => Task.FromResult(OutboxOperationResultModel.Completed(async (connection, transaction, token) =>
        {
            await persist(connection, transaction, token);
            throw new InvalidOperationException("persist failed");
        })));

        // Act
        var act = () => Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        (await ReadRowAsync(other)).PayloadJson.Should().Be("{\"original\":true}");
        (await ReadRowAsync(id)).Status.Should().Be(OutboxStatusEnum.InProgress);
    }

    [Fact]
    public async Task ItShouldRollBackPersistedWritesAndEndTheRunWithoutThrowingWhenPersistThrowsAnUnexpectedException()
    {
        // Arrange
        var id = await SeedAsync();
        var later = await SeedAsync();
        var other = await SeedAsync(availableAt: Now.AddHours(1));
        var persist = PersistPayload(other, "persisted");
        RespondWith(_ => Task.FromResult(OutboxOperationResultModel.Completed(async (connection, transaction, token) =>
        {
            await persist(connection, transaction, token);
            throw new IOException("disk failure");
        })));

        // Act
        var act = () => Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        await MockSynchroniser.Received(1).SynchroniseAsync(
            Arg.Any<OutboxOperationModel>(),
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<CancellationToken>());
        (await ReadRowAsync(other)).PayloadJson.Should().Be("{\"original\":true}");
        (await ReadRowAsync(id)).Status.Should().Be(OutboxStatusEnum.InProgress);
        (await ReadRowAsync(later)).Status.Should().Be(OutboxStatusEnum.Pending);
    }

    [Fact]
    public async Task ItShouldSwallowTheLostLeaseAndDiscardPersistedWritesWhenTheOperationLeaseWasLostBeforeCompletion()
    {
        // Arrange
        var id = await SeedAsync();
        var other = await SeedAsync(availableAt: Now.AddHours(1));
        var calls = 0;
        RespondWith(async _ =>
        {
            calls++;
            if (calls > 1)
            {
                return OutboxOperationResultModel.Completed();
            }

            await ExecuteSqlAsync(
                "UPDATE outboxoperations SET status = 'Pending', leaseowner = NULL, leaseexpiresat = NULL WHERE id = $id;",
                ("$id", id.ToString("D")));
            return OutboxOperationResultModel.Completed(PersistPayload(other, "persisted"));
        });

        // Act
        var act = () => Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        (await ReadRowAsync(other)).PayloadJson.Should().Be("{\"original\":true}");
        (await ReadRowAsync(id)).Status.Should().Be(OutboxStatusEnum.Completed);
        await MockSynchroniser.Received(2).SynchroniseAsync(
            Arg.Is<OutboxOperationModel>(operation => operation.Id == id),
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldProcessDueOperationsOldestFirstAndMarkThemCompleted()
    {
        // Arrange
        var newest = await SeedAsync(createdOn: Now.AddMinutes(-1));
        var oldest = await SeedAsync(createdOn: Now.AddMinutes(-30));
        var middle = await SeedAsync(createdOn: Now.AddMinutes(-10));
        var processed = new List<Guid>();
        RespondWith(operation =>
        {
            processed.Add(operation.Id);
            return Task.FromResult(OutboxOperationResultModel.Completed());
        });

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        processed.Should().Equal(oldest, middle, newest);
        foreach (var id in new[] { oldest, middle, newest })
        {
            var row = await ReadRowAsync(id);
            row.Status.Should().Be(OutboxStatusEnum.Completed);
            row.AttemptCount.Should().Be(1);
            row.LastError.Should().BeNull();
            row.LeaseOwner.Should().BeNull();
            row.LeaseExpiresAt.Should().BeNull();
        }

        (await Store.ListPendingAsync(UserId, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRunPersistInTheSameTransactionAsTheCompletion()
    {
        // Arrange
        var id = await SeedAsync();
        var other = await SeedAsync(availableAt: Now.AddHours(1));
        RespondWith(operation => Task.FromResult(operation.Id == id
            ? OutboxOperationResultModel.Completed(PersistPayload(other, "persisted"))
            : OutboxOperationResultModel.Completed()));

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        (await ReadRowAsync(id)).Status.Should().Be(OutboxStatusEnum.Completed);
        (await ReadRowAsync(other)).PayloadJson.Should().Be("persisted");
    }

    [Fact]
    public async Task ItShouldNotifyStateChangedOnceWhenThereIsNothingToProcess()
    {
        // Arrange
        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        MockNotifier.Received(1).NotifyStateChanged();
    }

    [Fact]
    public async Task ItShouldNotifyStateChangedAroundEachClaimAndOnceAtTheEndOfTheRun()
    {
        // Arrange
        await SeedAsync();
        await SeedAsync();

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        MockNotifier.Received(5).NotifyStateChanged();
    }

    [Fact]
    public async Task ItShouldInvokeTheSynchroniserOncePerOperationAndNeverConcurrentlyWhenTriggersOverlap()
    {
        // Arrange
        var first = await SeedAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var maximumActive = 0;
        var invocations = 0;
        RespondWith(async _ =>
        {
            var concurrent = Interlocked.Increment(ref active);
            InterlockedMax(ref maximumActive, concurrent);
            if (Interlocked.Increment(ref invocations) == 1)
            {
                started.SetResult();
                await gate.Task;
            }

            Interlocked.Decrement(ref active);
            return OutboxOperationResultModel.Completed();
        });
        var firstRun = Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);
        await started.Task;
        var second = await SeedAsync();

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Foreground, CancellationToken.None);
        Sut.Request(OutboxSyncTriggerEnum.WorkEnqueued);
        await Sut.RunAsync(OutboxSyncTriggerEnum.RetryDue, CancellationToken.None);
        gate.SetResult();
        await firstRun;

        // Assert
        invocations.Should().Be(2);
        maximumActive.Should().Be(1);
        MockSession.Received(2).Capture();
        (await ReadRowAsync(first)).Status.Should().Be(OutboxStatusEnum.Completed);
        (await ReadRowAsync(second)).Status.Should().Be(OutboxStatusEnum.Completed);
        await MockSynchroniser.Received(1).SynchroniseAsync(
            Arg.Is<OutboxOperationModel>(operation => operation.Id == first),
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<CancellationToken>());
        await MockSynchroniser.Received(1).SynchroniseAsync(
            Arg.Is<OutboxOperationModel>(operation => operation.Id == second),
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotCoalesceTriggersThatArriveAfterTheRunHasFinished()
    {
        // Arrange
        await SeedAsync();

        // Act
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);
        await Sut.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);

        // Assert
        MockSession.Received(2).Capture();
        await MockSynchroniser.Received(1).SynchroniseAsync(
            Arg.Any<OutboxOperationModel>(),
            Arg.Any<AccountSessionLeaseModel>(),
            Arg.Any<CancellationToken>());
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        do
        {
            current = Volatile.Read(ref target);
            if (value <= current)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref target, value, current) != current);
    }
}
