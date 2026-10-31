using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Offline.Synchronisers.CatchPhotographSynchroniserTests;

public sealed class WhenTestingSynchronise : BaseCatchPhotographSynchroniserTest
{
    [Fact]
    public async Task ItShouldCompleteAsANoOpWhenTheCatchIsNowADraft()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(2);
        await SyncMetadataOnlyAsync();
        await ResaveCatchAsync(catchModel, catchModel.Photographs, LocalCatchLifecycleEnum.Draft);

        // Act
        await RunAsync();

        // Assert
        foreach (var photo in catchModel.Photographs)
        {
            var operations = await ReadPhotographOperationsAsync(photo.Id);
            operations.Should().ContainSingle().Which.Status.Should().Be(OutboxStatusEnum.Completed);
            (await ReadPhotographStateAsync(photo.Id))!.Status.Should().Be(PhotographSyncStatusEnum.Pending);
        }

        await MockCatchSyncClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(), Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>());
        await MockCatchSyncClient.DidNotReceive().RecordPhotographAsync(
            Arg.Any<Guid>(), Arg.Any<RecordPhotographDto>(), Arg.Any<CancellationToken>());
        await MockUploadClient.DidNotReceive().UploadAsync(
            Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        MockFileSystem.DidNotReceive().DeleteFile(Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldCompleteAsANoOpWhenTheCatchWasRemovedLocally()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(2);
        await SyncMetadataOnlyAsync();
        await ExecuteSqlAsync("DELETE FROM localcatches WHERE id = $id;", ("$id", catchModel.Id.ToString("D")));

        // Act
        await RunAsync();

        // Assert
        foreach (var photo in catchModel.Photographs)
        {
            var operations = await ReadPhotographOperationsAsync(photo.Id);
            operations.Should().ContainSingle().Which.Status.Should().Be(OutboxStatusEnum.Completed);
        }

        await MockCatchSyncClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(), Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>());
        await MockUploadClient.DidNotReceive().UploadAsync(
            Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        MockFileSystem.DidNotReceive().DeleteFile(Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldWaitForTheCatchWhenItsMetadataIsNotSynchronised()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(2);
        await SyncMetadataOnlyAsync();
        await StateStore.ClearSyncedRevisionAsync(OwnerUserId, catchModel.Id, CancellationToken.None);

        // Act
        await RunAsync();

        // Assert
        foreach (var photo in catchModel.Photographs)
        {
            var operation = (await ReadPhotographOperationsAsync(photo.Id)).Single();
            operation.Status.Should().Be(OutboxStatusEnum.Pending);
            operation.LastError.Should().Be("WaitingForCatch");
            operation.AttemptCount.Should().Be(1);
            (await ReadPhotographStateAsync(photo.Id))!.Status.Should().Be(PhotographSyncStatusEnum.Pending);
        }

        await MockCatchSyncClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(), Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>());
        await MockUploadClient.DidNotReceive().UploadAsync(
            Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTerminateWithoutRequestingAnUrlWhenTheContentTypeIsMissing()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(1);
        await SyncMetadataOnlyAsync();
        await ExecuteSqlAsync(
            "UPDATE localcatchphotographs SET contenttype = NULL WHERE catchid = $id;",
            ("$id", catchModel.Id.ToString("D")));

        // Act
        await RunAsync();

        // Assert
        var operation = (await ReadPhotographOperationsAsync(catchModel.Photographs[0].Id)).Single();
        operation.Status.Should().Be(OutboxStatusEnum.Failed);
        operation.LastError.Should().Be("PhotographContentTypeMissing");
        await MockCatchSyncClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(), Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>());
        (await ReadStatusAsync(catchModel.Id)).State.Should().Be(CatchSyncStateEnum.ActionRequired);
    }

    [Fact]
    public async Task ItShouldTerminateAndKeepLocalDataWhenTheLocalFileIsMissing()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(3);
        var missing = catchModel.Photographs[1];
        var others = catchModel.Photographs.Where(photo => photo.Id != missing.Id).ToList();
        MissingFiles.Add(missing.AbsolutePath);

        // Act
        await RunAsync();

        // Assert
        var operation = (await ReadPhotographOperationsAsync(missing.Id)).Single();
        operation.Status.Should().Be(OutboxStatusEnum.Failed);
        operation.LastError.Should().Be("PhotographFileMissing");
        (await ReadPhotographStateAsync(missing.Id))!.Status.Should().Be(PhotographSyncStatusEnum.Pending);
        foreach (var photo in others)
        {
            (await ReadPhotographStateAsync(photo.Id))!.Status.Should().Be(PhotographSyncStatusEnum.Recorded);
        }

        await MockCatchSyncClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Is<PhotographUploadRequestDto>(request => request.PhotographId == missing.Id),
            Arg.Any<CancellationToken>());
        (await CountLocalPhotographRowsAsync(catchModel.Id)).Should().Be(3);
        LocalFiles.Should().ContainKey(missing.AbsolutePath);
        MockFileSystem.DidNotReceive().DeleteFile(Arg.Any<string>());
        (await ReadStatusAsync(catchModel.Id)).State.Should().Be(CatchSyncStateEnum.ActionRequired);
    }

    [Fact]
    public async Task ItShouldReleaseTheOperationAndStopTheRunWhenTheSessionExpiresRequestingAnUrl()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(3);
        foreach (var photo in catchModel.Photographs)
        {
            EnqueueCreateUrlFault(photo.Id, ApiOutcomeEnum.SessionExpired);
        }

        // Act
        await RunAsync();

        // Assert
        foreach (var photo in catchModel.Photographs)
        {
            var operation = (await ReadPhotographOperationsAsync(photo.Id)).Single();
            operation.Status.Should().Be(OutboxStatusEnum.Pending);
            operation.AttemptCount.Should().Be(0);
            operation.LastError.Should().BeNull();
            operation.LeaseOwner.Should().BeNull();
            (await ReadPhotographStateAsync(photo.Id))!.Status.Should().Be(PhotographSyncStatusEnum.Pending);
        }

        await MockCatchSyncClient.Received(1).CreatePhotographUploadAsync(
            Arg.Any<Guid>(), Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>());
        await MockUploadClient.DidNotReceive().UploadAsync(
            Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        (await SyncedRevisionAsync(catchModel.Id)).Should().Be(1);
    }

    [Fact]
    public async Task ItShouldTerminateWithoutRetryWhenCreatingAnUrlFailsValidationOrRecordingIsForbidden()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(3);
        var rejected = catchModel.Photographs[1];
        var forbidden = catchModel.Photographs[2];
        var fine = catchModel.Photographs[0];
        EnqueueCreateUrlFault(rejected.Id, ApiOutcomeEnum.ValidationFailed);
        EnqueueRecordFault(forbidden.Id, new RecordFaultModel(ApiOutcomeEnum.Forbidden, false));

        // Act
        await RunAsync();
        Advance(TimeSpan.FromHours(1));
        await RunAsync();

        // Assert
        var rejectedOperation = (await ReadPhotographOperationsAsync(rejected.Id)).Single();
        rejectedOperation.Status.Should().Be(OutboxStatusEnum.Failed);
        rejectedOperation.LastError.Should().Be("ValidationFailed");
        var forbiddenOperation = (await ReadPhotographOperationsAsync(forbidden.Id)).Single();
        forbiddenOperation.Status.Should().Be(OutboxStatusEnum.Failed);
        forbiddenOperation.LastError.Should().Be("Forbidden");
        (await ReadPhotographStateAsync(fine.Id))!.Status.Should().Be(PhotographSyncStatusEnum.Recorded);
        (await ReadPhotographStateAsync(forbidden.Id))!.Status.Should().Be(PhotographSyncStatusEnum.Uploaded);
        CountCalls("create", rejected.Id).Should().Be(1);
        CountCalls("create", forbidden.Id).Should().Be(1);
        CountCalls("put", forbidden.Id).Should().Be(1);
        CountCalls("record", forbidden.Id).Should().Be(1);
        (await ReadStatusAsync(catchModel.Id)).State.Should().Be(CatchSyncStateEnum.ActionRequired);
    }

    [Fact]
    public async Task ItShouldRetryFailedPhotographsAfterAManualResetWithoutRepeatingTheCompletedUpload()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(3);
        var rejected = catchModel.Photographs[1];
        var forbidden = catchModel.Photographs[2];
        EnqueueCreateUrlFault(rejected.Id, ApiOutcomeEnum.ValidationFailed);
        EnqueueRecordFault(forbidden.Id, new RecordFaultModel(ApiOutcomeEnum.Forbidden, false));
        await RunAsync();

        // Act
        var reset = await OutboxStore.ResetForRetryAsync(OwnerUserId, catchModel.Id, Now, CancellationToken.None);
        await RunAsync();

        // Assert
        reset.Should().Be(2);
        foreach (var photo in catchModel.Photographs)
        {
            (await ReadPhotographStateAsync(photo.Id))!.Status.Should().Be(PhotographSyncStatusEnum.Recorded);
            UploadedBytesByPhotograph[photo.Id].Should().Equal(photo.Bytes);
            ServerRecordCounts[photo.Id].Should().Be(1);
        }

        CountCalls("put", forbidden.Id).Should().Be(1);
        CountCalls("record", forbidden.Id).Should().Be(2);
        CountCalls("create", rejected.Id).Should().Be(2);
        (await ReadStatusAsync(catchModel.Id)).State.Should().Be(CatchSyncStateEnum.Synced);
    }

    [Fact]
    public async Task ItShouldKeepMetadataSyncedAndReportAttentionWhenAPhotographUploadIsInterrupted()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(3);
        var interrupted = catchModel.Photographs[1];
        EnqueueUploadFault(interrupted.Id, PhotographUploadOutcomeEnum.Unavailable);

        // Act
        await RunAsync();

        // Assert
        var state = await ReadPhotographStateAsync(interrupted.Id);
        state!.Status.Should().Be(PhotographSyncStatusEnum.Pending);
        state.ObjectKey.Should().BeNull();
        var operation = (await ReadPhotographOperationsAsync(interrupted.Id)).Single();
        operation.Status.Should().Be(OutboxStatusEnum.Pending);
        operation.AttemptCount.Should().Be(1);
        operation.LastError.Should().Be("Unavailable");
        operation.AvailableAt.Should().BeAfter(Now.AddSeconds(14));
        foreach (var photo in catchModel.Photographs.Where(photo => photo.Id != interrupted.Id))
        {
            (await ReadPhotographStateAsync(photo.Id))!.Status.Should().Be(PhotographSyncStatusEnum.Recorded);
        }

        (await SyncedRevisionAsync(catchModel.Id)).Should().Be(await LocalRevisionAsync(catchModel.Id));
        var status = await ReadStatusAsync(catchModel.Id);
        status.State.Should().Be(CatchSyncStateEnum.NeedsAttention);
        status.MetadataSynced.Should().BeTrue();
        status.PhotographsRecorded.Should().Be(2);
        status.PhotographCount.Should().Be(3);
        (await CountLocalPhotographRowsAsync(catchModel.Id)).Should().Be(3);
        MockFileSystem.DidNotReceive().DeleteFile(Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldRequestAFreshUrlAndUploadOnlyTheInterruptedPhotographWhenRetried()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(3);
        var interrupted = catchModel.Photographs[1];
        EnqueueUploadFault(interrupted.Id, PhotographUploadOutcomeEnum.Unavailable);
        await RunAsync();
        Advance(TimeSpan.FromMinutes(1));

        // Act
        await RunAsync();

        // Assert
        IssuedUrls[interrupted.Id].Should().HaveCount(2);
        IssuedUrls[interrupted.Id][1].Should().NotBe(IssuedUrls[interrupted.Id][0]);
        await MockUploadClient.Received(2).UploadAsync(
            Arg.Is<string>(url => url.Contains(interrupted.Id.ToString("D"))),
            Arg.Any<Stream>(),
            interrupted.ContentType,
            Arg.Any<CancellationToken>());
        foreach (var photo in catchModel.Photographs.Where(photo => photo.Id != interrupted.Id))
        {
            await MockUploadClient.Received(1).UploadAsync(
                Arg.Is<string>(url => url.Contains(photo.Id.ToString("D"))),
                Arg.Any<Stream>(),
                photo.ContentType,
                Arg.Any<CancellationToken>());
        }

        foreach (var photo in catchModel.Photographs)
        {
            (await ReadPhotographStateAsync(photo.Id))!.Status.Should().Be(PhotographSyncStatusEnum.Recorded);
            UploadedBytesByPhotograph[photo.Id].Should().Equal(photo.Bytes);
            ServerRecordCounts[photo.Id].Should().Be(1);
        }

        ServerCatches[catchModel.Id].Should().HaveCount(3);
        (await ReadPhotographOperationsAsync(interrupted.Id)).Single().Status.Should().Be(OutboxStatusEnum.Completed);
        (await ReadStatusAsync(catchModel.Id)).State.Should().Be(CatchSyncStateEnum.Synced);
    }

    [Fact]
    public async Task ItShouldNotTreatAnUploadedButUnrecordedPhotographAsComplete()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(3);
        var photo = catchModel.Photographs[1];
        EnqueueRecordFault(photo.Id, new RecordFaultModel(ApiOutcomeEnum.Unavailable, false));

        // Act
        await RunAsync();

        // Assert
        var state = await ReadPhotographStateAsync(photo.Id);
        state!.Status.Should().Be(PhotographSyncStatusEnum.Uploaded);
        state.ObjectKey.Should().Be(ObjectKeyFor(catchModel.Id, photo.Id));
        var operation = (await ReadPhotographOperationsAsync(photo.Id)).Single();
        operation.Status.Should().Be(OutboxStatusEnum.Pending);
        operation.LastError.Should().Be("Unavailable");
        var status = await ReadStatusAsync(catchModel.Id);
        status.State.Should().Be(CatchSyncStateEnum.NeedsAttention);
        status.PhotographsRecorded.Should().Be(2);
    }

    [Fact]
    public async Task ItShouldSkipTheUploadAndOnlyRecordWhenAPhotographWasUploadedButNotRecorded()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(3);
        var photo = catchModel.Photographs[1];
        EnqueueRecordFault(photo.Id, new RecordFaultModel(ApiOutcomeEnum.Unavailable, false));
        await RunAsync();
        Advance(TimeSpan.FromMinutes(1));

        // Act
        await RunAsync();

        // Assert
        await MockUploadClient.Received(1).UploadAsync(
            Arg.Is<string>(url => url.Contains(photo.Id.ToString("D"))),
            Arg.Any<Stream>(),
            photo.ContentType,
            Arg.Any<CancellationToken>());
        await MockUploadClient.Received(3).UploadAsync(
            Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await MockCatchSyncClient.Received(1).CreatePhotographUploadAsync(
            catchModel.Id,
            Arg.Is<PhotographUploadRequestDto>(request => request.PhotographId == photo.Id),
            Arg.Any<CancellationToken>());
        await MockCatchSyncClient.Received(2).RecordPhotographAsync(
            catchModel.Id,
            Arg.Is<RecordPhotographDto>(request =>
                request.PhotographId == photo.Id
                && request.ObjectKey == ObjectKeyFor(catchModel.Id, photo.Id)
                && request.ContentType == photo.ContentType),
            Arg.Any<CancellationToken>());
        var state = await ReadPhotographStateAsync(photo.Id);
        state!.Status.Should().Be(PhotographSyncStatusEnum.Recorded);
        state.ObjectKey.Should().Be(ObjectKeyFor(catchModel.Id, photo.Id));
        (await ReadStatusAsync(catchModel.Id)).State.Should().Be(CatchSyncStateEnum.Synced);
    }

    [Fact]
    public async Task ItShouldRequestANewUrlWhenTheUploadUrlWasRejected()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(3);
        var photo = catchModel.Photographs[1];
        EnqueueUploadFault(photo.Id, PhotographUploadOutcomeEnum.UrlRejected);
        await RunAsync();
        var firstState = await ReadPhotographStateAsync(photo.Id);
        var firstOperation = (await ReadPhotographOperationsAsync(photo.Id)).Single();
        Advance(TimeSpan.FromMinutes(1));

        // Act
        await RunAsync();

        // Assert
        firstState!.Status.Should().Be(PhotographSyncStatusEnum.Pending);
        firstOperation.Status.Should().Be(OutboxStatusEnum.Pending);
        firstOperation.LastError.Should().Be("UploadUrlRejected");
        firstOperation.AttemptCount.Should().Be(1);
        IssuedUrls[photo.Id].Should().HaveCount(2);
        IssuedUrls[photo.Id][1].Should().NotBe(IssuedUrls[photo.Id][0]);
        (await ReadPhotographStateAsync(photo.Id))!.Status.Should().Be(PhotographSyncStatusEnum.Recorded);
        UploadedBytesByPhotograph[photo.Id].Should().Equal(photo.Bytes);
        (await ReadStatusAsync(catchModel.Id)).State.Should().Be(CatchSyncStateEnum.Synced);
    }

    [Fact]
    public async Task ItShouldRecordIdempotentlyWithoutDuplicatesWhenTheRecordResponseWasLost()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(3);
        var photo = catchModel.Photographs[1];
        EnqueueRecordFault(photo.Id, new RecordFaultModel(ApiOutcomeEnum.Unavailable, true));
        await RunAsync();
        Advance(TimeSpan.FromMinutes(1));

        // Act
        await RunAsync();

        // Assert
        ServerCatches[catchModel.Id].Keys.Should().BeEquivalentTo(catchModel.Photographs.Select(item => item.Id));
        ServerRecordCounts[photo.Id].Should().Be(2);
        CountCalls("put", photo.Id).Should().Be(1);
        CountCalls("create", photo.Id).Should().Be(1);
        ObjectStore.Should().HaveCount(3);
        (await ReadPhotographStateAsync(photo.Id))!.Status.Should().Be(PhotographSyncStatusEnum.Recorded);
        (await ReadStatusAsync(catchModel.Id)).State.Should().Be(CatchSyncStateEnum.Synced);
    }

    [Fact]
    public async Task ItShouldMarkAPhotographDeadAfterTheMaximumAttemptsAndKeepOthersAndLocalBytes()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(3);
        var failing = catchModel.Photographs[1];
        PermanentUploadFaults[failing.Id] = PhotographUploadOutcomeEnum.Unavailable;

        // Act
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            await RunAsync();
            Advance(TimeSpan.FromHours(1));
        }

        await RunAsync();

        // Assert
        var operation = (await ReadPhotographOperationsAsync(failing.Id)).Single();
        operation.Status.Should().Be(OutboxStatusEnum.Dead);
        operation.AttemptCount.Should().Be(MaximumAttempts);
        operation.LastError.Should().Be("Unavailable");
        CountCalls("put", failing.Id).Should().Be(MaximumAttempts);
        foreach (var photo in catchModel.Photographs.Where(photo => photo.Id != failing.Id))
        {
            (await ReadPhotographStateAsync(photo.Id))!.Status.Should().Be(PhotographSyncStatusEnum.Recorded);
            CountCalls("put", photo.Id).Should().Be(1);
        }

        (await ReadPhotographStateAsync(failing.Id))!.Status.Should().Be(PhotographSyncStatusEnum.Pending);
        LocalFiles[failing.AbsolutePath].Should().Equal(failing.Bytes);
        (await CountLocalPhotographRowsAsync(catchModel.Id)).Should().Be(3);
        MockFileSystem.DidNotReceive().DeleteFile(Arg.Any<string>());
        var status = await ReadStatusAsync(catchModel.Id);
        status.State.Should().Be(CatchSyncStateEnum.NeedsAttention);
        status.CanRetry.Should().BeTrue();
        status.PhotographsRecorded.Should().Be(2);
    }

    [Fact]
    public async Task ItShouldSynchroniseADeadPhotographAfterAManualReset()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(3);
        var failing = catchModel.Photographs[1];
        PermanentUploadFaults[failing.Id] = PhotographUploadOutcomeEnum.Unavailable;
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            await RunAsync();
            Advance(TimeSpan.FromHours(1));
        }

        PermanentUploadFaults.Remove(failing.Id);

        // Act
        var reset = await OutboxStore.ResetForRetryAsync(OwnerUserId, catchModel.Id, Now, CancellationToken.None);
        await RunAsync();

        // Assert
        reset.Should().Be(1);
        (await ReadPhotographStateAsync(failing.Id))!.Status.Should().Be(PhotographSyncStatusEnum.Recorded);
        UploadedBytesByPhotograph[failing.Id].Should().Equal(failing.Bytes);
        ServerRecordCounts.Values.Should().OnlyContain(count => count == 1);
        ServerCatches[catchModel.Id].Should().HaveCount(3);
        (await ReadStatusAsync(catchModel.Id)).State.Should().Be(CatchSyncStateEnum.Synced);
    }

    [Fact]
    public async Task ItShouldReupsertTheCatchAndThenUploadWhenTheRemoteCatchIsMissing()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(2);
        var photo = catchModel.Photographs[1];
        EnqueueCreateUrlFault(photo.Id, ApiOutcomeEnum.NotFound);

        // Act
        await RunAsync();
        Advance(TimeSpan.FromMinutes(1));
        await RunAsync();

        // Assert
        await MockCatchSyncClient.Received(2).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == catchModel.Id && dto.Photographs.Count == 2),
            Arg.Any<CancellationToken>());
        CountCalls("create", photo.Id).Should().Be(2);
        (await ReadPhotographStateAsync(photo.Id))!.Status.Should().Be(PhotographSyncStatusEnum.Recorded);
        UploadedBytesByPhotograph[photo.Id].Should().Equal(photo.Bytes);
        ServerRecordCounts[photo.Id].Should().Be(1);
        ServerCatches[catchModel.Id].Should().HaveCount(2);
        (await SyncedRevisionAsync(catchModel.Id)).Should().Be(await LocalRevisionAsync(catchModel.Id));
        (await ReadStatusAsync(catchModel.Id)).State.Should().Be(CatchSyncStateEnum.Synced);
    }

    [Fact]
    public async Task ItShouldTerminateWhenTheRemoteCatchIsStillMissingAfterReupserting()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(2);
        var photo = catchModel.Photographs[1];
        EnqueueCreateUrlFault(photo.Id, ApiOutcomeEnum.NotFound, ApiOutcomeEnum.NotFound);

        // Act
        await RunAsync();
        Advance(TimeSpan.FromMinutes(1));
        await RunAsync();

        // Assert
        var operation = (await ReadPhotographOperationsAsync(photo.Id)).Single();
        operation.Status.Should().Be(OutboxStatusEnum.Failed);
        operation.LastError.Should().Be("NotFound");
        (await ReadStatusAsync(catchModel.Id)).State.Should().Be(CatchSyncStateEnum.ActionRequired);
    }

    [Fact]
    public async Task ItShouldKeepTheCatchRowAndRevisionUnchangedWhenOnlyThePhotographUploadFails()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(2);
        var failing = catchModel.Photographs[1];
        EnqueueCreateUrlFault(failing.Id, ApiOutcomeEnum.Unavailable);
        var before = await CatchStore.GetForOwnerAsync(OwnerUserId, catchModel.Id, CancellationToken.None);

        // Act
        await RunAsync();

        // Assert
        var after = await CatchStore.GetForOwnerAsync(OwnerUserId, catchModel.Id, CancellationToken.None);
        after!.Revision.Should().Be(before!.Revision);
        after.Photographs.Select(photo => photo.Id).Should().Equal(before.Photographs.Select(photo => photo.Id));
        (await SyncedRevisionAsync(catchModel.Id)).Should().Be(after.Revision);
        var status = await ReadStatusAsync(catchModel.Id);
        status.MetadataSynced.Should().BeTrue();
        status.State.Should().NotBe(CatchSyncStateEnum.Synced);
        await MockCatchSyncClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == catchModel.Id), Arg.Any<CancellationToken>());
        CountCalls("put", failing.Id).Should().Be(0);
    }

    [Fact]
    public async Task ItShouldNotUploadAPhotographReplacedBeforeItsUploadStarted()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(2);
        var removed = catchModel.Photographs[0];
        var kept = catchModel.Photographs[1];
        await SyncMetadataOnlyAsync();
        var added = NewPhoto(1, "image/png");
        await ResaveCatchAsync(catchModel, [kept, added]);

        // Act
        await RunAsync();

        // Assert
        var removedOperation = (await ReadPhotographOperationsAsync(removed.Id)).Single();
        removedOperation.Status.Should().Be(OutboxStatusEnum.Completed);
        await MockCatchSyncClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Is<PhotographUploadRequestDto>(request => request.PhotographId == removed.Id),
            Arg.Any<CancellationToken>());
        await MockUploadClient.DidNotReceive().UploadAsync(
            Arg.Is<string>(url => url.Contains(removed.Id.ToString("D"))),
            Arg.Any<Stream>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await MockCatchSyncClient.DidNotReceive().DeletePhotographAsync(
            Arg.Any<Guid>(), removed.Id, Arg.Any<CancellationToken>());
        (await ReadPhotographStateAsync(removed.Id)).Should().BeNull();
        foreach (var photo in new[] { kept, added })
        {
            (await ReadPhotographStateAsync(photo.Id))!.Status.Should().Be(PhotographSyncStatusEnum.Recorded);
            UploadedBytesByPhotograph[photo.Id].Should().Equal(photo.Bytes);
            ServerRecordCounts[photo.Id].Should().Be(1);
        }

        ServerCatches[catchModel.Id].Keys.Should().BeEquivalentTo(new[] { kept.Id, added.Id });
        (await ReadStatusAsync(catchModel.Id)).State.Should().Be(CatchSyncStateEnum.Synced);
    }

    [Fact]
    public async Task ItShouldDeleteTheServerPhotographWhenItIsRemovedWhileItsUploadIsInFlight()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(3);
        var removed = catchModel.Photographs[1];
        var remaining = catchModel.Photographs.Where(photo => photo.Id != removed.Id).ToList();
        var resaved = false;
        OnUpload = async photographId =>
        {
            if (photographId == removed.Id && !resaved)
            {
                resaved = true;
                await ResaveCatchAsync(catchModel, remaining);
            }
        };

        // Act
        await RunAsync();
        Advance(TimeSpan.FromMinutes(1));
        await RunAsync();

        // Assert
        resaved.Should().BeTrue();
        await MockCatchSyncClient.Received(1).DeletePhotographAsync(
            catchModel.Id, removed.Id, Arg.Any<CancellationToken>());
        foreach (var photo in remaining)
        {
            await MockCatchSyncClient.DidNotReceive().DeletePhotographAsync(
                Arg.Any<Guid>(), photo.Id, Arg.Any<CancellationToken>());
            (await ReadPhotographStateAsync(photo.Id))!.Status.Should().Be(PhotographSyncStatusEnum.Recorded);
            UploadedBytesByPhotograph[photo.Id].Should().Equal(photo.Bytes);
            ObjectStore.Should().ContainKey(ObjectKeyFor(catchModel.Id, photo.Id));
        }

        ServerCatches[catchModel.Id].Keys.Should().BeEquivalentTo(remaining.Select(photo => photo.Id));
        ObjectStore.Should().NotContainKey(ObjectKeyFor(catchModel.Id, removed.Id));
        (await ReadPhotographStateAsync(removed.Id)).Should().BeNull();
        CallLog.IndexOf($"delete:{removed.Id:D}").Should().BeGreaterThan(CallLog.IndexOf($"record:{removed.Id:D}"));
        CallLog.LastIndexOf("upsert").Should().BeGreaterThan(CallLog.IndexOf($"delete:{removed.Id:D}"));
        (await CountLocalPhotographRowsAsync(catchModel.Id)).Should().Be(2);
        LocalFiles.Should().ContainKey(removed.AbsolutePath);
        MockFileSystem.DidNotReceive().DeleteFile(Arg.Any<string>());
        var status = await ReadStatusAsync(catchModel.Id);
        status.State.Should().Be(CatchSyncStateEnum.Synced);
        status.PhotographCount.Should().Be(2);
        status.PhotographsRecorded.Should().Be(2);
    }

    [Fact]
    public async Task ItShouldRecoverAnInterruptedPhotographOperationFromAnotherProcessWithoutDuplicates()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(2);
        var photo = catchModel.Photographs[0];
        await SyncMetadataOnlyAsync();
        await ExecuteSqlAsync(
            """
            UPDATE outboxoperations
            SET status = 'InProgress', leaseowner = 'crashed-process', leaseexpiresat = $expires, attemptcount = 1
            WHERE aggregateid = $id AND operationtype = 'UploadCatchPhotograph';
            """,
            ("$id", photo.Id.ToString("D")),
            ("$expires", Now.AddMinutes(5).ToString("O")));
        var restarted = CreateCoordinator();

        // Act
        await RunAsync(restarted);

        // Assert
        var operation = (await ReadPhotographOperationsAsync(photo.Id)).Single();
        operation.Status.Should().Be(OutboxStatusEnum.Completed);
        operation.LeaseOwner.Should().BeNull();
        ServerCatches[catchModel.Id].Should().HaveCount(2);
        foreach (var item in catchModel.Photographs)
        {
            ServerRecordCounts[item.Id].Should().Be(1);
            (await ReadPhotographStateAsync(item.Id))!.Status.Should().Be(PhotographSyncStatusEnum.Recorded);
            UploadedBytesByPhotograph[item.Id].Should().Equal(item.Bytes);
        }

        (await ReadStatusAsync(catchModel.Id)).State.Should().Be(CatchSyncStateEnum.Synced);
    }

    [Fact]
    public async Task ItShouldOnlyRecordWhenAnotherProcessAlreadyUploadedThePhotographBeforeBeingKilled()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(2);
        var photo = catchModel.Photographs[0];
        await SyncMetadataOnlyAsync();
        var key = ObjectKeyFor(catchModel.Id, photo.Id);
        ObjectStore[key] = photo.Bytes;
        await StateStore.SetPhotographUploadedAsync(
            new PhotographSyncStateModel
            {
                PhotographId = photo.Id,
                CatchId = catchModel.Id,
                UserId = OwnerUserId,
                Status = PhotographSyncStatusEnum.Uploaded,
                ObjectKey = key
            },
            Now,
            CancellationToken.None);
        await ExecuteSqlAsync(
            """
            UPDATE outboxoperations
            SET status = 'InProgress', leaseowner = 'crashed-process', leaseexpiresat = $expires, attemptcount = 1
            WHERE aggregateid = $id AND operationtype = 'UploadCatchPhotograph';
            """,
            ("$id", photo.Id.ToString("D")),
            ("$expires", Now.AddMinutes(5).ToString("O")));
        var restarted = CreateCoordinator();

        // Act
        await RunAsync(restarted);

        // Assert
        CountCalls("create", photo.Id).Should().Be(0);
        CountCalls("put", photo.Id).Should().Be(0);
        ServerRecordCounts[photo.Id].Should().Be(1);
        (await ReadPhotographStateAsync(photo.Id))!.Status.Should().Be(PhotographSyncStatusEnum.Recorded);
        ServerCatches[catchModel.Id].Should().HaveCount(2);
        (await ReadStatusAsync(catchModel.Id)).State.Should().Be(CatchSyncStateEnum.Synced);
    }

    [Fact]
    public async Task ItShouldCreateAnUrlUploadTheBytesAndRecordEachPhotographWithConsistentIdentifiers()
    {
        // Arrange
        var catchModel = await SaveCatchAsync(3);

        // Act
        await RunAsync();

        // Assert
        foreach (var photo in catchModel.Photographs)
        {
            var key = ObjectKeyFor(catchModel.Id, photo.Id);
            Received.InOrder(() =>
            {
                MockCatchSyncClient.CreatePhotographUploadAsync(
                    catchModel.Id,
                    Arg.Is<PhotographUploadRequestDto>(request =>
                        request.PhotographId == photo.Id && request.ContentType == photo.ContentType),
                    Arg.Any<CancellationToken>());
                MockUploadClient.UploadAsync(
                    IssuedUrls[photo.Id][0],
                    Arg.Any<Stream>(),
                    photo.ContentType,
                    Arg.Any<CancellationToken>());
                MockCatchSyncClient.RecordPhotographAsync(
                    catchModel.Id,
                    Arg.Is<RecordPhotographDto>(request =>
                        request.PhotographId == photo.Id
                        && request.ObjectKey == key
                        && request.ContentType == photo.ContentType),
                    Arg.Any<CancellationToken>());
            });
            CallsFor(photo.Id).Should().Equal($"create:{photo.Id:D}", $"put:{photo.Id:D}", $"record:{photo.Id:D}");
            IssuedUrls[photo.Id].Should().ContainSingle().Which.Should().Contain(key);
            ObjectStore[key].Should().Equal(photo.Bytes);
            UploadedBytesByPhotograph[photo.Id].Should().Equal(photo.Bytes);
            var state = await ReadPhotographStateAsync(photo.Id);
            state!.Status.Should().Be(PhotographSyncStatusEnum.Recorded);
            state.ObjectKey.Should().Be(key);
            var operation = (await ReadPhotographOperationsAsync(photo.Id)).Single();
            operation.Status.Should().Be(OutboxStatusEnum.Completed);
            operation.LastError.Should().BeNull();
            ServerRecordCounts[photo.Id].Should().Be(1);
        }

        ServerCatches[catchModel.Id].Should().HaveCount(3);
        await MockCatchSyncClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == catchModel.Id && dto.Photographs.Count == 3),
            Arg.Any<CancellationToken>());
        await MockCatchSyncClient.Received(3).CreatePhotographUploadAsync(
            catchModel.Id, Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>());
        await MockCatchSyncClient.Received(3).RecordPhotographAsync(
            catchModel.Id, Arg.Any<RecordPhotographDto>(), Arg.Any<CancellationToken>());
        await MockUploadClient.Received(3).UploadAsync(
            Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        MockFileSystem.DidNotReceive().DeleteFile(Arg.Any<string>());
        var status = await ReadStatusAsync(catchModel.Id);
        status.State.Should().Be(CatchSyncStateEnum.Synced);
        status.MetadataSynced.Should().BeTrue();
        status.PhotographCount.Should().Be(3);
        status.PhotographsRecorded.Should().Be(3);
    }
}
