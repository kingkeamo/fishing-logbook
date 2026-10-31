using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Tests.Support;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Offline.Stores.SqliteCatchSyncStatusStoreTests;

public sealed class WhenTestingList : BaseSqliteCatchSyncStatusStoreTest
{
    [Fact]
    public async Task ItShouldReturnNoStatusesWhenThereAreNoCatches()
    {
        // Arrange
        await Harness.Database.EnsureInitializedAsync(CancellationToken.None);

        // Act
        var statuses = await Sut.ListAsync(OwnerUserId, CancellationToken.None);

        // Assert
        statuses.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldExcludeDraftCatches()
    {
        // Arrange
        var draft = await SaveCatchAsync(lifecycle: LocalCatchLifecycleEnum.Draft);
        var saved = await SaveCatchAsync();

        // Act
        var statuses = await Sut.ListAsync(OwnerUserId, CancellationToken.None);

        // Assert
        statuses.Keys.Should().BeEquivalentTo([saved.Id]);
        statuses.ContainsKey(draft.Id).Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotReturnAnotherUsersCatches()
    {
        // Arrange
        var mine = await SaveCatchAsync();
        var theirs = await SaveCatchAsync(store: Harness.CreateCatchStore(OtherUserId));

        // Act
        var ownerStatuses = await Sut.ListAsync(OwnerUserId, CancellationToken.None);
        var otherStatuses = await Sut.ListAsync(OtherUserId, CancellationToken.None);

        // Assert
        ownerStatuses.Keys.Should().BeEquivalentTo([mine.Id]);
        otherStatuses.Keys.Should().BeEquivalentTo([theirs.Id]);
    }

    [Fact]
    public async Task ItShouldReportWaitingForAPendingOperationThatHasNeverBeenAttempted()
    {
        // Arrange
        var args = await SaveCatchAsync(photographCount: 1);

        // Act
        var status = await StatusOfAsync(args.Id);

        // Assert
        status.State.Should().Be(CatchSyncStateEnum.WaitingToSync);
        status.MetadataSynced.Should().BeFalse();
        status.PhotographCount.Should().Be(1);
        status.PhotographsRecorded.Should().Be(0);
        status.CanRetry.Should().BeFalse();
        status.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldReportSynchronisingForAnInProgressOperation()
    {
        // Arrange
        var args = await SaveCatchAsync();
        await SetMetadataOperationAsync(args.Id, OutboxStatusEnum.InProgress, attemptCount: 1);

        // Act
        var status = await StatusOfAsync(args.Id);

        // Assert
        status.State.Should().Be(CatchSyncStateEnum.Synchronising);
        status.CanRetry.Should().BeFalse();
        status.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldReportNeedsAttentionWithTheRetryTimeForAPendingOperationThatHasBeenAttempted()
    {
        // Arrange
        var args = await SaveCatchAsync();
        await SetMetadataOperationAsync(
            args.Id,
            OutboxStatusEnum.Pending,
            attemptCount: 2,
            availableAt: Now.AddMinutes(7));

        // Act
        var status = await StatusOfAsync(args.Id);

        // Assert
        status.State.Should().Be(CatchSyncStateEnum.NeedsAttention);
        status.CanRetry.Should().BeTrue();
        status.NextAttemptAt.Should().Be(Now.AddMinutes(7));
    }

    [Fact]
    public async Task ItShouldReportNeedsAttentionWithoutARetryTimeForADeadOperation()
    {
        // Arrange
        var args = await SaveCatchAsync();
        await SetMetadataOperationAsync(args.Id, OutboxStatusEnum.Dead, attemptCount: 5);

        // Act
        var status = await StatusOfAsync(args.Id);

        // Assert
        status.State.Should().Be(CatchSyncStateEnum.NeedsAttention);
        status.CanRetry.Should().BeTrue();
        status.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldReportActionRequiredForAFailedOperation()
    {
        // Arrange
        var args = await SaveCatchAsync();
        await SetMetadataOperationAsync(args.Id, OutboxStatusEnum.Failed, attemptCount: 1);

        // Act
        var status = await StatusOfAsync(args.Id);

        // Assert
        status.State.Should().Be(CatchSyncStateEnum.ActionRequired);
        status.CanRetry.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldReportSyncedWhenTheSyncedRevisionIsCurrentAndEveryPhotographIsRecorded()
    {
        // Arrange
        var args = await SaveCatchAsync(photographCount: 2);
        await SetMetadataOperationAsync(args.Id, OutboxStatusEnum.Completed, attemptCount: 1);
        await RecordMetadataAsync(args.Id, 1);
        foreach (var photograph in args.Photographs)
        {
            await RecordPhotographAsync(args.Id, photograph.Id);
        }

        // Act
        var status = await StatusOfAsync(args.Id);

        // Assert
        status.State.Should().Be(CatchSyncStateEnum.Synced);
        status.MetadataSynced.Should().BeTrue();
        status.PhotographCount.Should().Be(2);
        status.PhotographsRecorded.Should().Be(2);
        status.CanRetry.Should().BeFalse();
        status.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldNotReportSyncedWhenMetadataIsSyncedButPhotographsAreNotAllRecorded()
    {
        // Arrange
        var args = await SaveCatchAsync(photographCount: 2);
        await SetMetadataOperationAsync(args.Id, OutboxStatusEnum.Completed, attemptCount: 1);
        await RecordMetadataAsync(args.Id, 1);
        await RecordPhotographAsync(args.Id, args.Photographs[0].Id);

        // Act
        var status = await StatusOfAsync(args.Id);

        // Assert
        status.State.Should().Be(CatchSyncStateEnum.WaitingToSync);
        status.MetadataSynced.Should().BeTrue();
        status.PhotographCount.Should().Be(2);
        status.PhotographsRecorded.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldNotReportSyncedWhenTheCatchWasEditedAfterItsRevisionWasSynced()
    {
        // Arrange
        var args = await SaveCatchAsync();
        await RecordMetadataAsync(args.Id, 1);
        await SaveCatchAsync(existing: args);

        // Act
        var status = await StatusOfAsync(args.Id);

        // Assert
        status.MetadataSynced.Should().BeFalse();
        status.State.Should().Be(CatchSyncStateEnum.WaitingToSync);
    }

    [Fact]
    public async Task ItShouldReportActionRequiredWhenAPhotographUploadFailedEvenIfMetadataIsSyncedAndWaiting()
    {
        // Arrange
        var args = await SaveCatchAsync(photographCount: 1);
        await SetMetadataOperationAsync(args.Id, OutboxStatusEnum.Completed);
        await RecordMetadataAsync(args.Id, 1);
        await AddPhotographOperationAsync(args.Photographs[0].Id, OutboxStatusEnum.Failed, attemptCount: 1);

        // Act
        var status = await StatusOfAsync(args.Id);

        // Assert
        status.State.Should().Be(CatchSyncStateEnum.ActionRequired);
        status.MetadataSynced.Should().BeTrue();
        status.CanRetry.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldLetActionRequiredWinOverNeedsAttention()
    {
        // Arrange
        var args = await SaveCatchAsync(photographCount: 1);
        await SetMetadataOperationAsync(args.Id, OutboxStatusEnum.Pending, attemptCount: 1);
        await AddPhotographOperationAsync(args.Photographs[0].Id, OutboxStatusEnum.Failed, attemptCount: 1);

        // Act
        var status = await StatusOfAsync(args.Id);

        // Assert
        status.State.Should().Be(CatchSyncStateEnum.ActionRequired);
    }

    [Fact]
    public async Task ItShouldLetNeedsAttentionWinOverSynchronising()
    {
        // Arrange
        var args = await SaveCatchAsync(photographCount: 1);
        await SetMetadataOperationAsync(args.Id, OutboxStatusEnum.InProgress, attemptCount: 1);
        await AddPhotographOperationAsync(args.Photographs[0].Id, OutboxStatusEnum.Pending, attemptCount: 2);

        // Act
        var status = await StatusOfAsync(args.Id);

        // Assert
        status.State.Should().Be(CatchSyncStateEnum.NeedsAttention);
    }

    [Fact]
    public async Task ItShouldLetSynchronisingWinOverWaiting()
    {
        // Arrange
        var args = await SaveCatchAsync(photographCount: 1);
        await AddPhotographOperationAsync(args.Photographs[0].Id, OutboxStatusEnum.InProgress, attemptCount: 1);

        // Act
        var status = await StatusOfAsync(args.Id);

        // Assert
        status.State.Should().Be(CatchSyncStateEnum.Synchronising);
    }

    [Fact]
    public async Task ItShouldReportTheEarliestRetryTimeAcrossRetryingOperations()
    {
        // Arrange
        var args = await SaveCatchAsync(photographCount: 1);
        await SetMetadataOperationAsync(
            args.Id,
            OutboxStatusEnum.Pending,
            attemptCount: 1,
            availableAt: Now.AddMinutes(10));
        await AddPhotographOperationAsync(
            args.Photographs[0].Id,
            OutboxStatusEnum.Pending,
            attemptCount: 1,
            availableAt: Now.AddMinutes(5));

        // Act
        var status = await StatusOfAsync(args.Id);

        // Assert
        status.NextAttemptAt.Should().Be(Now.AddMinutes(5));
    }

    [Fact]
    public async Task ItShouldUseTheLatestOperationPerKeyWhenAnOlderFailedOperationWasSupersededByAPendingOne()
    {
        // Arrange
        var args = await SaveCatchAsync();
        await SetMetadataOperationAsync(
            args.Id,
            OutboxStatusEnum.Failed,
            attemptCount: 3,
            createdOn: Now.AddDays(-1));
        await OutboxRowFixture.InsertAsync(
            Harness.ConnectionFactory,
            OwnerUserId,
            args.Id,
            OutboxStatusEnum.Pending,
            createdOn: Now,
            availableAt: Now);

        // Act
        var status = await StatusOfAsync(args.Id);

        // Assert
        status.State.Should().Be(CatchSyncStateEnum.WaitingToSync);
        status.CanRetry.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotReportActionRequiredWhenAnOlderFailedOperationWasSupersededByACompletedOne()
    {
        // Arrange
        var args = await SaveCatchAsync();
        await SetMetadataOperationAsync(
            args.Id,
            OutboxStatusEnum.Failed,
            attemptCount: 3,
            createdOn: Now.AddDays(-1));
        await OutboxRowFixture.InsertAsync(
            Harness.ConnectionFactory,
            OwnerUserId,
            args.Id,
            OutboxStatusEnum.Completed,
            createdOn: Now,
            availableAt: Now);
        await RecordMetadataAsync(args.Id, 1);

        // Act
        var status = await StatusOfAsync(args.Id);

        // Assert
        status.State.Should().Be(CatchSyncStateEnum.Synced);
        status.CanRetry.Should().BeFalse();
    }
}
