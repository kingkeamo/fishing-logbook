using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Tests.Support;

namespace FishingLogBook.Mobile.Tests.Common.Offline.SqliteOutboxStoreTests;

public sealed class WhenTestingResetForRetry : BaseSqliteOutboxLeaseTest
{
    [Fact]
    public async Task ItShouldReturnZeroAndChangeNothingWhenTheCatchHasNoOperations()
    {
        // Arrange
        var other = await SeedAsync(status: OutboxStatusEnum.Failed);

        // Act
        var reset = await Sut.ResetForRetryAsync(UserId, Guid.NewGuid(), Now, CancellationToken.None);

        // Assert
        reset.Should().Be(0);
        (await ReadAsync(other)).Status.Should().Be(OutboxStatusEnum.Failed);
    }

    [Fact]
    public async Task ItShouldNotResetOperationsOfAnotherUserForTheSameCatchId()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var foreign = await SeedAsync(userId: OtherUserId, aggregateId: catchId, status: OutboxStatusEnum.Failed);

        // Act
        var reset = await Sut.ResetForRetryAsync(UserId, catchId, Now, CancellationToken.None);

        // Assert
        reset.Should().Be(0);
        (await ReadAsync(foreign)).Status.Should().Be(OutboxStatusEnum.Failed);
    }

    [Fact]
    public async Task ItShouldNotTouchOperationsOfOtherCatches()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var otherCatchFailed = await SeedAsync(status: OutboxStatusEnum.Failed, attemptCount: 3);
        var otherCatchPending = await SeedAsync(attemptCount: 3, availableAt: Now.AddHours(1));
        var otherPhotographId = Guid.NewGuid();
        await OutboxRowFixture.InsertPhotographSyncAsync(ConnectionFactory, UserId, Guid.NewGuid(), otherPhotographId);
        var otherPhoto = await SeedAsync(
            aggregateId: otherPhotographId,
            aggregateType: OutboxAggregateTypeEnum.CatchPhotograph,
            operationType: OutboxOperationTypeEnum.UploadCatchPhotograph,
            status: OutboxStatusEnum.Dead);

        // Act
        var reset = await Sut.ResetForRetryAsync(UserId, catchId, Now, CancellationToken.None);

        // Assert
        reset.Should().Be(0);
        (await ReadAsync(otherCatchFailed)).Status.Should().Be(OutboxStatusEnum.Failed);
        var pending = await ReadAsync(otherCatchPending);
        pending.AttemptCount.Should().Be(3);
        pending.AvailableAt.Should().Be(Now.AddHours(1));
        (await ReadAsync(otherPhoto)).Status.Should().Be(OutboxStatusEnum.Dead);
    }

    [Fact]
    public async Task ItShouldNotResurrectACatchWhoseLatestOperationIsCompleted()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var older = await SeedAsync(
            aggregateId: catchId,
            status: OutboxStatusEnum.Failed,
            createdOn: Now.AddHours(-2));
        var latest = await SeedAsync(
            aggregateId: catchId,
            status: OutboxStatusEnum.Completed,
            createdOn: Now.AddHours(-1));

        // Act
        var reset = await Sut.ResetForRetryAsync(UserId, catchId, Now, CancellationToken.None);

        // Assert
        reset.Should().Be(0);
        (await ReadAsync(older)).Status.Should().Be(OutboxStatusEnum.Failed);
        (await ReadAsync(latest)).Status.Should().Be(OutboxStatusEnum.Completed);
    }

    [Fact]
    public async Task ItShouldResetOnlyTheLatestFailedRowWhenOlderFailedRowsExistForTheSameKey()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var older = await SeedAsync(
            aggregateId: catchId,
            status: OutboxStatusEnum.Failed,
            createdOn: Now.AddHours(-2));
        var latest = await SeedAsync(
            aggregateId: catchId,
            status: OutboxStatusEnum.Dead,
            createdOn: Now.AddHours(-1));

        // Act
        var reset = await Sut.ResetForRetryAsync(UserId, catchId, Now, CancellationToken.None);

        // Assert
        reset.Should().Be(1);
        (await ReadAsync(older)).Status.Should().Be(OutboxStatusEnum.Failed);
        (await ReadAsync(latest)).Status.Should().Be(OutboxStatusEnum.Pending);
    }

    [Fact]
    public async Task ItShouldMakeAPendingOperationWithAttemptsImmediatelyDueWithAFreshAttemptCount()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var id = await SeedAsync(
            aggregateId: catchId,
            attemptCount: 4,
            availableAt: Now.AddHours(2));

        // Act
        var reset = await Sut.ResetForRetryAsync(UserId, catchId, Now, CancellationToken.None);

        // Assert
        reset.Should().Be(1);
        var row = await ReadAsync(id);
        row.Status.Should().Be(OutboxStatusEnum.Pending);
        row.AttemptCount.Should().Be(0);
        row.AvailableAt.Should().Be(Now);
        row.LastError.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldResetFailedAndDeadOperationsOfTheCatchAndItsPhotographs()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        await Database.EnsureInitializedAsync(CancellationToken.None);
        await OutboxRowFixture.InsertPhotographSyncAsync(ConnectionFactory, UserId, catchId, photographId);
        var metadata = await SeedAsync(
            aggregateId: catchId,
            status: OutboxStatusEnum.Failed,
            attemptCount: 5,
            availableAt: Now.AddDays(1));
        var photo = await SeedAsync(
            aggregateId: photographId,
            aggregateType: OutboxAggregateTypeEnum.CatchPhotograph,
            operationType: OutboxOperationTypeEnum.UploadCatchPhotograph,
            status: OutboxStatusEnum.Dead,
            attemptCount: 7);

        // Act
        var reset = await Sut.ResetForRetryAsync(UserId, catchId, Now, CancellationToken.None);

        // Assert
        reset.Should().Be(2);
        var metadataRow = await ReadAsync(metadata);
        metadataRow.Status.Should().Be(OutboxStatusEnum.Pending);
        metadataRow.AttemptCount.Should().Be(0);
        metadataRow.AvailableAt.Should().Be(Now);
        var photoRow = await ReadAsync(photo);
        photoRow.Status.Should().Be(OutboxStatusEnum.Pending);
        photoRow.AttemptCount.Should().Be(0);
        photoRow.AvailableAt.Should().Be(Now);
        (await Sut.ListPendingAsync(UserId, CancellationToken.None)).Should().HaveCount(2);
    }
}
