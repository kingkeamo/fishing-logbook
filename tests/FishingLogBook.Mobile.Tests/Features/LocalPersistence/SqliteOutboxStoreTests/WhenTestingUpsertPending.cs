using AwesomeAssertions;
using FishingLogBook.Mobile.Features.LocalPersistence.Enums;
using FishingLogBook.Mobile.Features.LocalPersistence.Models;

namespace FishingLogBook.Mobile.Tests.Features.LocalPersistence.SqliteOutboxStoreTests;

public sealed class WhenTestingUpsertPending : BaseSqliteOutboxStoreTest
{
    [Fact]
    public async Task ItShouldCoalescePendingOperationsForTheSameAggregateAndType()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        await Database.EnsureInitializedAsync(CancellationToken.None);

        // Act
        await Database.ExecuteInTransactionAsync(
            async (connection, transaction, ct) =>
            {
                await Sut.UpsertPendingAsync(
                    connection,
                    transaction,
                    new OutboxOperationModel
                    {
                        Id = Guid.NewGuid(),
                        UserId = UserId,
                        AggregateType = OutboxAggregateTypeEnum.Catch,
                        AggregateId = aggregateId,
                        OperationType = OutboxOperationTypeEnum.UpsertCatchMetadata,
                        PayloadJson = "{\"revision\":1}",
                        Status = OutboxStatusEnum.Pending,
                        AttemptCount = 0,
                        CreatedOn = DateTimeOffset.UtcNow,
                        AvailableAt = DateTimeOffset.UtcNow
                    },
                    ct);
            },
            CancellationToken.None);
        await Database.ExecuteInTransactionAsync(
            async (connection, transaction, ct) =>
            {
                await Sut.UpsertPendingAsync(
                    connection,
                    transaction,
                    new OutboxOperationModel
                    {
                        Id = Guid.NewGuid(),
                        UserId = UserId,
                        AggregateType = OutboxAggregateTypeEnum.Catch,
                        AggregateId = aggregateId,
                        OperationType = OutboxOperationTypeEnum.UpsertCatchMetadata,
                        PayloadJson = "{\"revision\":2}",
                        Status = OutboxStatusEnum.Pending,
                        AttemptCount = 0,
                        CreatedOn = DateTimeOffset.UtcNow,
                        AvailableAt = DateTimeOffset.UtcNow
                    },
                    ct);
            },
            CancellationToken.None);
        var pending = await Sut.ListPendingForAggregateAsync(
            UserId,
            OutboxAggregateTypeEnum.Catch,
            aggregateId,
            CancellationToken.None);

        // Assert
        pending.Should().HaveCount(1);
        pending[0].PayloadJson.Should().Contain("\"revision\":2");
    }

    [Fact]
    public async Task ItShouldNotReturnPendingOperationsOwnedByAnotherUser()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var otherUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        await Database.EnsureInitializedAsync(CancellationToken.None);
        await Database.ExecuteInTransactionAsync(
            async (connection, transaction, ct) =>
            {
                await Sut.UpsertPendingAsync(
                    connection,
                    transaction,
                    new OutboxOperationModel
                    {
                        Id = Guid.NewGuid(),
                        UserId = UserId,
                        AggregateType = OutboxAggregateTypeEnum.Catch,
                        AggregateId = aggregateId,
                        OperationType = OutboxOperationTypeEnum.UpsertCatchMetadata,
                        PayloadJson = "{}",
                        Status = OutboxStatusEnum.Pending,
                        AttemptCount = 0,
                        CreatedOn = DateTimeOffset.UtcNow,
                        AvailableAt = DateTimeOffset.UtcNow
                    },
                    ct);
            },
            CancellationToken.None);

        // Act
        var otherPending = await Sut.ListPendingAsync(otherUserId, CancellationToken.None);
        var ownerPending = await Sut.ListPendingAsync(UserId, CancellationToken.None);

        // Assert
        otherPending.Should().BeEmpty();
        ownerPending.Should().HaveCount(1);
    }
}
