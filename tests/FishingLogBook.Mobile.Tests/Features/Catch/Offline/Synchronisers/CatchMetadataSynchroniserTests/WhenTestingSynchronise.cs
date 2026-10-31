using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Common.Offline.Constants;
using FishingLogBook.Mobile.Common.Offline.Coordinators;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Offline.Synchronisers.CatchMetadataSynchroniserTests;

public sealed class WhenTestingSynchronise : BaseCatchMetadataSynchroniserTest
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ItShouldFailTerminallyWithoutARequestWhenAPhotographContentTypeIsMissing(string? contentType)
    {
        // Arrange
        var catchId = await SaveCatchAsync(CreateSaveArgs(
            Guid.NewGuid(),
            [CreatePhotograph(0, "image/jpeg"), CreatePhotograph(1, contentType)]));

        // Act
        await RunAsync();
        await RunAsync();

        // Assert
        var operation = (await ReadMetadataOperationsAsync(catchId)).Single();
        operation.Status.Should().Be(OutboxStatusEnum.Failed);
        operation.LastError.Should().Be(OutboxErrorCodeConstants.PhotographContentTypeMissing);
        MockClient.ReceivedCalls().Should().BeEmpty();
        ServerCatches.Should().BeEmpty();
        (await ReadSyncedRevisionAsync(catchId)).Should().BeNull();
        (await ReadLocalCatchAsync(UserA, catchId)).Photographs.Should().HaveCount(2);
        (await ReadPhotographOperationsAsync()).Should().BeEmpty();
        (await ReadStatusAsync(UserA, catchId)).State.Should().Be(CatchSyncStateEnum.ActionRequired);
    }

    [Theory]
    [InlineData(ApiOutcomeEnum.ValidationFailed)]
    [InlineData(ApiOutcomeEnum.Forbidden)]
    public async Task ItShouldFailTerminallyAndNotRetryUntilUserRetriesWhenTheServerRejectsTheCatch(
        ApiOutcomeEnum outcome)
    {
        // Arrange
        var catchId = await SaveCatchAsync(CreateSaveArgs(Guid.NewGuid()));
        UpsertRejection = _ => outcome;

        // Act
        await RunAsync();
        Clock.Advance(TimeSpan.FromHours(1));
        await RunAsync();

        // Assert
        var failed = (await ReadMetadataOperationsAsync(catchId)).Single();
        failed.Status.Should().Be(OutboxStatusEnum.Failed);
        failed.LastError.Should().Be(outcome.ToString());
        failed.AttemptCount.Should().Be(1);
        await MockClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == catchId),
            Arg.Any<CancellationToken>());
        ServerCatches.Should().BeEmpty();
        var local = await ReadLocalCatchAsync(UserA, catchId);
        local.Revision.Should().Be(1);
        local.Photographs.Should().HaveCount(2);
        (await ReadSyncedRevisionAsync(catchId)).Should().BeNull();
        var status = await ReadStatusAsync(UserA, catchId);
        status.State.Should().Be(CatchSyncStateEnum.ActionRequired);
        status.CanRetry.Should().BeTrue();
        status.MetadataSynced.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldSyncAfterTheUserRetriesAFailedCatchOnceTheServerAccepts()
    {
        // Arrange
        var catchId = await SaveCatchAsync(CreateSaveArgs(Guid.NewGuid()));
        UpsertRejection = _ => ApiOutcomeEnum.ValidationFailed;
        await RunAsync();
        UpsertRejection = null;

        // Act
        var reset = await OutboxStore.ResetForRetryAsync(UserA, catchId, Clock.GetUtcNow(), CancellationToken.None);
        await RunAsync();

        // Assert
        reset.Should().Be(1);
        var operations = await ReadMetadataOperationsAsync(catchId);
        operations.Should().ContainSingle().Which.Status.Should().Be(OutboxStatusEnum.Completed);
        await MockClient.Received(2).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == catchId),
            Arg.Any<CancellationToken>());
        ServerCatches.Should().ContainSingle().Which.Key.Should().Be(catchId);
        (await ReadSyncedRevisionAsync(catchId)).Should().Be(1);
        var status = await ReadStatusAsync(UserA, catchId);
        status.MetadataSynced.Should().BeTrue();
        status.CanRetry.Should().BeFalse();
        status.State.Should().Be(CatchSyncStateEnum.WaitingToSync);
    }

    [Theory]
    [InlineData(ApiOutcomeEnum.Unavailable)]
    [InlineData(ApiOutcomeEnum.ServerError)]
    [InlineData(ApiOutcomeEnum.InvalidResponse)]
    public async Task ItShouldRetryWithExponentialBackoffThenGoDeadAfterMaximumAttemptsAndRecoverOnUserRetry(
        ApiOutcomeEnum outcome)
    {
        // Arrange
        var catchId = await SaveCatchAsync(CreateSaveArgs(Guid.NewGuid()));
        UpsertRejection = _ => outcome;

        // Act and Assert for every retryable attempt
        for (var attempt = 1; attempt < MaximumAttempts; attempt++)
        {
            var attemptedAt = Clock.GetUtcNow();
            await RunAsync();

            var pending = (await ReadMetadataOperationsAsync(catchId)).Single();
            pending.Status.Should().Be(OutboxStatusEnum.Pending);
            pending.AttemptCount.Should().Be(attempt);
            pending.LastError.Should().Be(outcome.ToString());
            pending.AvailableAt.Should().Be(attemptedAt + DelayForAttempt(attempt));
            await MockClient.Received(attempt).UpsertAsync(
                Arg.Is<CatchDto>(dto => dto.Id == catchId),
                Arg.Any<CancellationToken>());

            Clock.Advance(DelayForAttempt(attempt) - TimeSpan.FromSeconds(1));
            await RunAsync();
            await MockClient.Received(attempt).UpsertAsync(
                Arg.Is<CatchDto>(dto => dto.Id == catchId),
                Arg.Any<CancellationToken>());
            Clock.Advance(TimeSpan.FromSeconds(1));
        }

        await RunAsync();

        // Assert exhaustion
        var dead = (await ReadMetadataOperationsAsync(catchId)).Single();
        dead.Status.Should().Be(OutboxStatusEnum.Dead);
        dead.AttemptCount.Should().Be(MaximumAttempts);
        dead.LastError.Should().Be(outcome.ToString());
        await MockClient.Received(MaximumAttempts).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == catchId),
            Arg.Any<CancellationToken>());
        ServerCatches.Should().BeEmpty();
        var local = await ReadLocalCatchAsync(UserA, catchId);
        local.Revision.Should().Be(1);
        local.Photographs.Should().HaveCount(2);
        var status = await ReadStatusAsync(UserA, catchId);
        status.State.Should().Be(CatchSyncStateEnum.NeedsAttention);
        status.CanRetry.Should().BeTrue();

        // Act: dead operations are not retried automatically, only by the user
        Clock.Advance(TimeSpan.FromHours(1));
        await RunAsync();
        await MockClient.Received(MaximumAttempts).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == catchId),
            Arg.Any<CancellationToken>());
        UpsertRejection = null;
        await OutboxStore.ResetForRetryAsync(UserA, catchId, Clock.GetUtcNow(), CancellationToken.None);
        await RunAsync();

        // Assert revival
        var revived = (await ReadMetadataOperationsAsync(catchId)).Single();
        revived.Status.Should().Be(OutboxStatusEnum.Completed);
        revived.AttemptCount.Should().Be(1);
        ServerCatches.Should().ContainSingle().Which.Key.Should().Be(catchId);
        (await ReadSyncedRevisionAsync(catchId)).Should().Be(1);
    }

    [Fact]
    public async Task ItShouldPauseWithoutCountingAnAttemptOrProcessingLaterCatchesWhenTheSessionExpires()
    {
        // Arrange
        var firstCatchId = await SaveCatchAsync(CreateSaveArgs(Guid.NewGuid()));
        Clock.Advance(TimeSpan.FromSeconds(1));
        var secondCatchId = await SaveCatchAsync(CreateSaveArgs(Guid.NewGuid()));
        UpsertRejection = _ => ApiOutcomeEnum.SessionExpired;

        // Act
        await RunAsync();

        // Assert the paused run
        var first = (await ReadMetadataOperationsAsync(firstCatchId)).Single();
        first.Status.Should().Be(OutboxStatusEnum.Pending);
        first.AttemptCount.Should().Be(0);
        first.LastError.Should().BeNull();
        first.LeaseOwner.Should().BeNull();
        var second = (await ReadMetadataOperationsAsync(secondCatchId)).Single();
        second.Status.Should().Be(OutboxStatusEnum.Pending);
        second.AttemptCount.Should().Be(0);
        await MockClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == firstCatchId),
            Arg.Any<CancellationToken>());
        await MockClient.DidNotReceive().UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == secondCatchId),
            Arg.Any<CancellationToken>());
        (await ReadOutboxAsync()).Should().NotContain(row =>
            row.Status == OutboxStatusEnum.Failed || row.Status == OutboxStatusEnum.Dead);
        (await ReadStatusAsync(UserA, firstCatchId)).CanRetry.Should().BeFalse();

        // Act: authentication is back
        UpsertRejection = null;
        await RunAsync();

        // Assert
        ServerCatches.Keys.Should().BeEquivalentTo([firstCatchId, secondCatchId]);
        (await ReadMetadataOperationsAsync(firstCatchId)).Single().Status.Should().Be(OutboxStatusEnum.Completed);
        (await ReadMetadataOperationsAsync(secondCatchId)).Single().Status.Should().Be(OutboxStatusEnum.Completed);
        (await ReadSyncedRevisionAsync(firstCatchId)).Should().Be(1);
        (await ReadSyncedRevisionAsync(secondCatchId)).Should().Be(1);
    }

    [Fact]
    public async Task ItShouldKeepTheServerPhotographAndRetryWithoutUpsertingWhenDeletingARemovedPhotographFails()
    {
        // Arrange
        var keptPhotograph = CreatePhotograph(0, "image/jpeg");
        var removedPhotograph = CreatePhotograph(1, "image/png");
        var catchId = await SaveCatchAsync(CreateSaveArgs(Guid.NewGuid(), [keptPhotograph, removedPhotograph]));
        await RunAsync();
        await MarkPhotographRecordedAsync(UserA, catchId, keptPhotograph.Id);
        await MarkPhotographRecordedAsync(UserA, catchId, removedPhotograph.Id);
        await SaveCatchAsync(CreateSaveArgs(catchId, [keptPhotograph]));
        MockClient.ClearReceivedCalls();
        DeleteRejection = (_, _) => ApiOutcomeEnum.Unavailable;

        // Act
        await RunAsync();

        // Assert
        var pending = (await ReadMetadataOperationsAsync(catchId)).Single(row => row.Status == OutboxStatusEnum.Pending);
        pending.AttemptCount.Should().Be(1);
        pending.LastError.Should().Be(ApiOutcomeEnum.Unavailable.ToString());
        await MockClient.Received(1).DeletePhotographAsync(catchId, removedPhotograph.Id, Arg.Any<CancellationToken>());
        await MockClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        (await SyncStateStore.GetPhotographStateAsync(UserA, removedPhotograph.Id, CancellationToken.None))
            .Should().NotBeNull();
        (await ReadSyncedRevisionAsync(catchId)).Should().Be(1);
        ServerCatches[catchId].Photographs.Select(photograph => photograph.Id)
            .Should().BeEquivalentTo([keptPhotograph.Id, removedPhotograph.Id]);
    }

    [Fact]
    public async Task ItShouldKeepExactlyOneServerCatchWhenTheResponseIsLostAndTheRequestIsRetried()
    {
        // Arrange
        var catchId = await SaveCatchAsync(CreateSaveArgs(Guid.NewGuid()));
        var lose = true;
        UpsertLostResponse = _ => lose ? ApiOutcomeEnum.Unavailable : null;

        // Act
        await RunAsync();

        // Assert the first run: stored on the server, unknown locally
        var afterLoss = (await ReadMetadataOperationsAsync(catchId)).Single();
        afterLoss.Status.Should().Be(OutboxStatusEnum.Pending);
        afterLoss.AttemptCount.Should().Be(1);
        afterLoss.AvailableAt.Should().Be(StartTime + DelayForAttempt(1));
        ServerCatches.Should().ContainSingle();
        (await ReadSyncedRevisionAsync(catchId)).Should().BeNull();
        (await ReadStatusAsync(UserA, catchId)).MetadataSynced.Should().BeFalse();
        (await ReadPhotographOperationsAsync()).Should().BeEmpty();

        // Act: not due yet, then due
        lose = false;
        await RunAsync();
        await MockClient.Received(1).UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        Clock.Advance(DelayForAttempt(1));
        await RunAsync();

        // Assert
        await MockClient.Received(2).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == catchId),
            Arg.Any<CancellationToken>());
        ServerCatches.Should().ContainSingle().Which.Key.Should().Be(catchId);
        var completed = (await ReadMetadataOperationsAsync(catchId)).Single();
        completed.Status.Should().Be(OutboxStatusEnum.Completed);
        completed.AttemptCount.Should().Be(2);
        (await ReadSyncedRevisionAsync(catchId)).Should().Be(1);
        (await ReadPhotographOperationsAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task ItShouldNotCallTheApiWhileOfflineAndSyncOnceBackOnline()
    {
        // Arrange
        Online = false;
        var catchId = await SaveCatchAsync(CreateSaveArgs(Guid.NewGuid()));

        // Act
        await RunAsync();

        // Assert the offline state
        MockClient.ReceivedCalls().Should().BeEmpty();
        var queued = (await ReadMetadataOperationsAsync(catchId)).Single();
        queued.Status.Should().Be(OutboxStatusEnum.Pending);
        queued.AttemptCount.Should().Be(0);
        ServerCatches.Should().BeEmpty();
        (await ReadLocalCatchAsync(UserA, catchId)).Lifecycle.Should().Be(LocalCatchLifecycleEnum.Saved);

        // Act: connectivity returns
        Online = true;
        await RunAsync();

        // Assert
        await MockClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == catchId),
            Arg.Any<CancellationToken>());
        ServerCatches.Should().ContainSingle().Which.Key.Should().Be(catchId);
        (await ReadSyncedRevisionAsync(catchId)).Should().Be(1);
    }

    [Fact]
    public async Task ItShouldCompleteWithoutARequestWhenTheRevisionIsAlreadySyncedAndSendAgainAfterAnEdit()
    {
        // Arrange
        var photographs = new[] { CreatePhotograph(0), CreatePhotograph(1, "image/png") };
        var catchId = await SaveCatchAsync(CreateSaveArgs(Guid.NewGuid(), photographs));
        await RunAsync();
        await InsertPendingMetadataOperationAsync(UserA, catchId);

        // Act
        await RunAsync();

        // Assert the duplicate operation sends nothing
        await MockClient.Received(1).UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        var operations = await ReadMetadataOperationsAsync(catchId);
        operations.Should().HaveCount(2);
        operations.Should().OnlyContain(row => row.Status == OutboxStatusEnum.Completed);
        (await ReadSyncedRevisionAsync(catchId)).Should().Be(1);
        (await ReadPhotographOperationsAsync()).Should().HaveCount(2);

        // Act: an edit creates a new revision
        await SaveCatchAsync(CreateSaveArgs(catchId, photographs, "Edited notes"));
        await RunAsync();

        // Assert
        await MockClient.Received(2).UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        await MockClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == catchId && dto.Notes == "Edited notes"),
            Arg.Any<CancellationToken>());
        ServerCatches[catchId].Notes.Should().Be("Edited notes");
        (await ReadSyncedRevisionAsync(catchId)).Should().Be(2);
    }

    [Fact]
    public async Task ItShouldCompleteWithoutARequestWhenTheCatchIsADraft()
    {
        // Arrange
        var catchId = await SaveCatchAsync(CreateSaveArgs(Guid.NewGuid(), lifecycle: LocalCatchLifecycleEnum.Draft));

        // Act
        await RunAsync();

        // Assert
        (await ReadMetadataOperationsAsync(catchId)).Single().Status.Should().Be(OutboxStatusEnum.Completed);
        await MockClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        ServerCatches.Should().BeEmpty();
        (await ReadSyncedRevisionAsync(catchId)).Should().BeNull();
        (await ReadPhotographOperationsAsync()).Should().BeEmpty();
        (await ReadLocalCatchAsync(UserA, catchId)).Lifecycle.Should().Be(LocalCatchLifecycleEnum.Draft);
    }

    [Fact]
    public async Task ItShouldCompleteWithoutARequestWhenTheLocalCatchNoLongerExists()
    {
        // Arrange
        var missingCatchId = Guid.NewGuid();
        await InsertPendingMetadataOperationAsync(UserA, missingCatchId);

        // Act
        await RunAsync();

        // Assert
        (await ReadMetadataOperationsAsync(missingCatchId)).Single().Status.Should().Be(OutboxStatusEnum.Completed);
        MockClient.ReceivedCalls().Should().BeEmpty();
        (await ReadSyncedRevisionAsync(missingCatchId)).Should().BeNull();
        (await ReadPhotographOperationsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldOnlySyncTheSignedInAccountsCatchesAndLeaveOtherAccountsOperationsPending()
    {
        // Arrange
        var catchForA = await SaveCatchAsync(CreateSaveArgs(Guid.NewGuid()));
        CurrentUserId = UserB;
        var catchForB = await SaveCatchAsync(CreateSaveArgs(Guid.NewGuid()));
        CurrentUserId = UserA;

        // Act
        await RunAsync();

        // Assert only A was processed
        await MockClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == catchForA && dto.RecordedByUserId == UserA),
            Arg.Any<CancellationToken>());
        await MockClient.DidNotReceive().UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == catchForB),
            Arg.Any<CancellationToken>());
        ServerCatches.Keys.Should().BeEquivalentTo([catchForA]);
        var operationForB = (await ReadMetadataOperationsAsync(catchForB)).Single();
        operationForB.UserId.Should().Be(UserB);
        operationForB.Status.Should().Be(OutboxStatusEnum.Pending);
        operationForB.AttemptCount.Should().Be(0);
        (await ReadSyncedRevisionAsync(catchForB)).Should().BeNull();
        (await ReadPhotographOperationsAsync()).Should().OnlyContain(row => row.UserId == UserA);

        // Act: the session switches to B
        CurrentUserId = UserB;
        await RunAsync();

        // Assert
        await MockClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == catchForB && dto.RecordedByUserId == UserB),
            Arg.Any<CancellationToken>());
        await MockClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == catchForA),
            Arg.Any<CancellationToken>());
        ServerCatches.Keys.Should().BeEquivalentTo([catchForA, catchForB]);
        (await ReadSyncedRevisionAsync(catchForB)).Should().Be(1);
    }

    [Fact]
    public async Task ItShouldSyncARecoveredOperationWhenTheProcessDiedBeforeSendingTheRequest()
    {
        // Arrange
        var catchId = await SaveCatchAsync(CreateSaveArgs(Guid.NewGuid()));
        var claimed = await OutboxStore.ClaimNextAsync(
            UserA,
            "killed-process-owner",
            Clock.GetUtcNow(),
            OutboxSyncCoordinator.LeaseDuration,
            CancellationToken.None);
        claimed.Should().NotBeNull();
        (await ReadMetadataOperationsAsync(catchId)).Single().Status.Should().Be(OutboxStatusEnum.InProgress);
        var restartedCoordinator = CreateCoordinator();

        // Act
        await restartedCoordinator.RunAsync(OutboxSyncTriggerEnum.SessionOnline, CancellationToken.None);

        // Assert
        var operation = (await ReadMetadataOperationsAsync(catchId)).Single();
        operation.Status.Should().Be(OutboxStatusEnum.Completed);
        operation.AttemptCount.Should().Be(2);
        operation.LeaseOwner.Should().BeNull();
        await MockClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == catchId),
            Arg.Any<CancellationToken>());
        ServerCatches.Should().ContainSingle().Which.Key.Should().Be(catchId);
        (await ReadSyncedRevisionAsync(catchId)).Should().Be(1);
    }

    [Fact]
    public async Task ItShouldKeepOneServerCatchWhenTheProcessDiedAfterTheServerStoredItButBeforeCompletionCommitted()
    {
        // Arrange
        var catchId = await SaveCatchAsync(CreateSaveArgs(Guid.NewGuid()));
        await OutboxStore.ClaimNextAsync(
            UserA,
            "killed-process-owner",
            Clock.GetUtcNow(),
            OutboxSyncCoordinator.LeaseDuration,
            CancellationToken.None);
        StoreOnServer(await ReadLocalCatchAsync(UserA, catchId));
        (await ReadSyncedRevisionAsync(catchId)).Should().BeNull();
        var restartedCoordinator = CreateCoordinator();

        // Act
        await restartedCoordinator.RunAsync(OutboxSyncTriggerEnum.SessionOnline, CancellationToken.None);

        // Assert
        ServerCatches.Should().ContainSingle().Which.Key.Should().Be(catchId);
        ServerCatches[catchId].Photographs.Should().HaveCount(2);
        (await ReadMetadataOperationsAsync(catchId)).Single().Status.Should().Be(OutboxStatusEnum.Completed);
        (await ReadSyncedRevisionAsync(catchId)).Should().Be(1);
        await MockClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == catchId),
            Arg.Any<CancellationToken>());
        (await ReadPhotographOperationsAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task ItShouldNotCallDeleteForARemovedPhotographThatWasNeverUploaded()
    {
        // Arrange
        var keptPhotograph = CreatePhotograph(0, "image/jpeg");
        var removedPhotograph = CreatePhotograph(1, "image/png");
        var catchId = await SaveCatchAsync(CreateSaveArgs(Guid.NewGuid(), [keptPhotograph, removedPhotograph]));
        await RunAsync();
        await SaveCatchAsync(CreateSaveArgs(catchId, [keptPhotograph]));
        MockClient.ClearReceivedCalls();

        // Act
        await RunAsync();

        // Assert
        await MockClient.DidNotReceive().DeletePhotographAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        (await SyncStateStore.GetPhotographStateAsync(UserA, removedPhotograph.Id, CancellationToken.None))
            .Should().BeNull();
        ServerCatches[catchId].Photographs.Select(photograph => photograph.Id)
            .Should().BeEquivalentTo([keptPhotograph.Id]);
        (await ReadSyncedRevisionAsync(catchId)).Should().Be(2);
    }

    [Fact]
    public async Task ItShouldTreatANotFoundDeleteAsDoneAndStillUpsertWhenTheServerPhotographIsAlreadyGone()
    {
        // Arrange
        var keptPhotograph = CreatePhotograph(0, "image/jpeg");
        var removedPhotograph = CreatePhotograph(1, "image/png");
        var catchId = await SaveCatchAsync(CreateSaveArgs(Guid.NewGuid(), [keptPhotograph, removedPhotograph]));
        await RunAsync();
        await MarkPhotographRecordedAsync(UserA, catchId, keptPhotograph.Id);
        await MarkPhotographRecordedAsync(UserA, catchId, removedPhotograph.Id);
        await SaveCatchAsync(CreateSaveArgs(catchId, [keptPhotograph]));
        DeleteRejection = (_, _) => ApiOutcomeEnum.NotFound;
        MockClient.ClearReceivedCalls();

        // Act
        await RunAsync();

        // Assert
        await MockClient.Received(1).DeletePhotographAsync(catchId, removedPhotograph.Id, Arg.Any<CancellationToken>());
        await MockClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == catchId && dto.Photographs.Count == 1),
            Arg.Any<CancellationToken>());
        (await SyncStateStore.GetPhotographStateAsync(UserA, removedPhotograph.Id, CancellationToken.None))
            .Should().BeNull();
        ServerCatches[catchId].Photographs.Select(photograph => photograph.Id)
            .Should().BeEquivalentTo([keptPhotograph.Id]);
        (await ReadSyncedRevisionAsync(catchId)).Should().Be(2);
    }

    [Fact]
    public async Task ItShouldDeleteTheRemovedServerPhotographBeforeUpsertingTheCatch()
    {
        // Arrange
        var keptPhotograph = CreatePhotograph(0, "image/jpeg");
        var removedPhotograph = CreatePhotograph(1, "image/png");
        var catchId = await SaveCatchAsync(CreateSaveArgs(Guid.NewGuid(), [keptPhotograph, removedPhotograph]));
        await RunAsync();
        await MarkPhotographRecordedAsync(UserA, catchId, keptPhotograph.Id);
        await MarkPhotographRecordedAsync(UserA, catchId, removedPhotograph.Id);
        await SaveCatchAsync(CreateSaveArgs(catchId, [keptPhotograph]));
        MockClient.ClearReceivedCalls();

        // Act
        await RunAsync();

        // Assert
        Received.InOrder(() =>
        {
            MockClient.DeletePhotographAsync(catchId, removedPhotograph.Id, Arg.Any<CancellationToken>());
            MockClient.UpsertAsync(
                Arg.Is<CatchDto>(dto => dto.Id == catchId
                    && dto.Photographs.Count == 1
                    && dto.Photographs[0].Id == keptPhotograph.Id),
                Arg.Any<CancellationToken>());
        });
        await MockClient.Received(1).DeletePhotographAsync(
            catchId,
            removedPhotograph.Id,
            Arg.Any<CancellationToken>());
        await MockClient.DidNotReceive().DeletePhotographAsync(
            catchId,
            keptPhotograph.Id,
            Arg.Any<CancellationToken>());
        (await SyncStateStore.GetPhotographStateAsync(UserA, removedPhotograph.Id, CancellationToken.None))
            .Should().BeNull();
        (await SyncStateStore.GetPhotographStateAsync(UserA, keptPhotograph.Id, CancellationToken.None))
            .Should().NotBeNull();
        ServerCatches.Should().ContainSingle();
        ServerCatches[catchId].Photographs.Select(photograph => photograph.Id)
            .Should().BeEquivalentTo([keptPhotograph.Id]);
        (await ReadSyncedRevisionAsync(catchId)).Should().Be(2);
    }

    [Fact]
    public async Task ItShouldNeverOverwriteAnEditMadeWhileTheRequestIsInFlightAndSendTheNewRevisionNext()
    {
        // Arrange
        var photographs = new[] { CreatePhotograph(0), CreatePhotograph(1, "image/png") };
        var catchId = await SaveCatchAsync(CreateSaveArgs(Guid.NewGuid(), photographs, "Original notes"));
        UpsertInFlight = async _ =>
        {
            UpsertInFlight = null;
            await SaveCatchAsync(CreateSaveArgs(catchId, photographs, "Edited while syncing"));
            Online = false;
        };

        // Act
        await RunAsync();

        // Assert after the run that sent revision 1 while revision 2 was saved
        var local = await ReadLocalCatchAsync(UserA, catchId);
        local.Notes.Should().Be("Edited while syncing");
        local.Revision.Should().Be(2);
        (await ReadSyncedRevisionAsync(catchId)).Should().Be(1);
        ServerCatches.Should().ContainSingle();
        ServerCatches[catchId].Notes.Should().Be("Original notes");
        var operations = await ReadMetadataOperationsAsync(catchId);
        operations.Should().HaveCount(2);
        operations.Count(row => row.Status == OutboxStatusEnum.Completed).Should().Be(1);
        operations.Count(row => row.Status == OutboxStatusEnum.Pending).Should().Be(1);
        var inFlightStatus = await ReadStatusAsync(UserA, catchId);
        inFlightStatus.State.Should().NotBe(CatchSyncStateEnum.Synced);
        inFlightStatus.MetadataSynced.Should().BeFalse();

        // Act: back online
        Online = true;
        await RunAsync();

        // Assert
        await MockClient.Received(2).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == catchId),
            Arg.Any<CancellationToken>());
        await MockClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == catchId && dto.Notes == "Edited while syncing"),
            Arg.Any<CancellationToken>());
        ServerCatches.Should().ContainSingle();
        ServerCatches[catchId].Notes.Should().Be("Edited while syncing");
        var finalLocal = await ReadLocalCatchAsync(UserA, catchId);
        finalLocal.Notes.Should().Be("Edited while syncing");
        finalLocal.Revision.Should().Be(2);
        (await ReadSyncedRevisionAsync(catchId)).Should().Be(2);
        (await ReadMetadataOperationsAsync(catchId)).Should().OnlyContain(row => row.Status == OutboxStatusEnum.Completed);
        (await ReadPhotographOperationsAsync()).Should().HaveCount(2);
        (await ReadStatusAsync(UserA, catchId)).MetadataSynced.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldNeverOverwriteAnEditMadeWhileTheRequestIsInFlightWhenTheSameRunContinues()
    {
        // Arrange
        var photographs = new[] { CreatePhotograph(0), CreatePhotograph(1, "image/png") };
        var catchId = await SaveCatchAsync(CreateSaveArgs(Guid.NewGuid(), photographs, "Original notes"));
        UpsertInFlight = async _ =>
        {
            UpsertInFlight = null;
            await SaveCatchAsync(CreateSaveArgs(catchId, photographs, "Edited while syncing"));
        };

        // Act
        await RunAsync();

        // Assert
        await MockClient.Received(2).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == catchId),
            Arg.Any<CancellationToken>());
        ServerCatches.Should().ContainSingle();
        ServerCatches[catchId].Notes.Should().Be("Edited while syncing");
        (await ReadLocalCatchAsync(UserA, catchId)).Notes.Should().Be("Edited while syncing");
        (await ReadSyncedRevisionAsync(catchId)).Should().Be(2);
        (await ReadMetadataOperationsAsync(catchId)).Should().OnlyContain(row => row.Status == OutboxStatusEnum.Completed);
    }

    [Fact]
    public async Task ItShouldUpsertTheCatchOnceAndQueueOnePendingUploadPerPhotographWhenSavedOnline()
    {
        // Arrange
        var first = CreatePhotograph(0, "image/jpeg");
        var second = CreatePhotograph(1, "image/png");
        var catchId = await SaveCatchAsync(CreateSaveArgs(Guid.NewGuid(), [first, second]));
        var local = await ReadLocalCatchAsync(UserA, catchId);

        // Act
        await RunAsync();

        // Assert the request
        await MockClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto =>
                dto.Id == catchId
                && dto.CaughtOn == local.CaughtOn
                && dto.SpeciesName == "Brown trout"
                && dto.Weight == 1.2m
                && dto.Length == 42m
                && dto.Method == "Fly"
                && dto.BaitOrLure == "Dry fly"
                && dto.Notes == "Evening rise"
                && dto.PlaceName == "Lough Corrib"
                && dto.CaughtByUserId == UserA
                && dto.RecordedByUserId == UserA
                && dto.Photographs.Count == 2
                && dto.Photographs[0].Id == first.Id
                && dto.Photographs[0].CatchId == catchId
                && dto.Photographs[0].ContentType == "image/jpeg"
                && dto.Photographs[1].Id == second.Id
                && dto.Photographs[1].ContentType == "image/png"),
            Arg.Any<CancellationToken>());
        await MockClient.DidNotReceive().DeletePhotographAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());

        // Assert the server state
        var serverCatch = ServerCatches.Should().ContainSingle().Which.Value;
        serverCatch.Photographs.Select(photograph => (photograph.Id, photograph.ContentType))
            .Should().Equal((first.Id, "image/jpeg"), (second.Id, "image/png"));

        // Assert the local state
        var metadata = (await ReadMetadataOperationsAsync(catchId)).Single();
        metadata.Status.Should().Be(OutboxStatusEnum.Completed);
        metadata.AttemptCount.Should().Be(1);
        metadata.LastError.Should().BeNull();
        (await ReadSyncedRevisionAsync(catchId)).Should().Be(1);
        var uploads = await ReadPhotographOperationsAsync();
        uploads.Should().HaveCount(2);
        uploads.Should().OnlyContain(row =>
            row.Status == OutboxStatusEnum.Pending
            && row.AggregateType == OutboxAggregateTypeEnum.CatchPhotograph
            && row.AttemptCount == 0
            && row.UserId == UserA
            && row.PayloadJson.Contains(catchId.ToString("D")));
        uploads.Select(row => row.AggregateId).Should().BeEquivalentTo([first.Id, second.Id]);
        var states = await SyncStateStore.ListPhotographStatesAsync(UserA, catchId, CancellationToken.None);
        states.Should().HaveCount(2);
        states.Should().OnlyContain(state => state.Status == PhotographSyncStatusEnum.Pending && state.ObjectKey == null);
        states.Select(state => state.PhotographId).Should().BeEquivalentTo([first.Id, second.Id]);
        var afterSync = await ReadLocalCatchAsync(UserA, catchId);
        afterSync.Revision.Should().Be(1);
        afterSync.Photographs.Should().HaveCount(2);
    }

    [Fact]
    public async Task ItShouldReportWaitingBeforeAndWaitingWithMetadataSyncedAfterTheMetadataRequest()
    {
        // Arrange
        var catchId = await SaveCatchAsync(CreateSaveArgs(Guid.NewGuid()));
        var before = await ReadStatusAsync(UserA, catchId);

        // Act
        await RunAsync();

        // Assert
        before.State.Should().Be(CatchSyncStateEnum.WaitingToSync);
        before.MetadataSynced.Should().BeFalse();
        before.PhotographCount.Should().Be(2);
        before.PhotographsRecorded.Should().Be(0);
        var after = await ReadStatusAsync(UserA, catchId);
        after.State.Should().Be(CatchSyncStateEnum.WaitingToSync);
        after.MetadataSynced.Should().BeTrue();
        after.PhotographCount.Should().Be(2);
        after.PhotographsRecorded.Should().Be(0);
        after.CanRetry.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNeverReportSyncedWhileAnyPhotographIsUnrecorded()
    {
        // Arrange
        var first = CreatePhotograph(0);
        var second = CreatePhotograph(1, "image/png");
        var catchId = await SaveCatchAsync(CreateSaveArgs(Guid.NewGuid(), [first, second]));
        await RunAsync();

        // Act
        var noneRecorded = await ReadStatusAsync(UserA, catchId);
        await MarkPhotographRecordedAsync(UserA, catchId, first.Id);
        var oneRecorded = await ReadStatusAsync(UserA, catchId);
        await MarkPhotographRecordedAsync(UserA, catchId, second.Id);
        var allRecorded = await ReadStatusAsync(UserA, catchId);

        // Assert
        noneRecorded.State.Should().NotBe(CatchSyncStateEnum.Synced);
        noneRecorded.PhotographsRecorded.Should().Be(0);
        oneRecorded.State.Should().NotBe(CatchSyncStateEnum.Synced);
        oneRecorded.MetadataSynced.Should().BeTrue();
        oneRecorded.PhotographsRecorded.Should().Be(1);
        oneRecorded.PhotographCount.Should().Be(2);
        allRecorded.State.Should().Be(CatchSyncStateEnum.Synced);
        allRecorded.PhotographsRecorded.Should().Be(2);
        allRecorded.CanRetry.Should().BeFalse();
    }
}
