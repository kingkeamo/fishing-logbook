using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Models;

namespace FishingLogBook.Mobile.Tests.Common.Offline.SqliteOutboxStoreTests;

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
    public async Task ItShouldCreateASeparatePendingRowWhenAnEarlierClaimIsInProgress()
    {
        // Arrange — simulates sync claiming revision 1 while the user later saves revision 2
        var aggregateId = Guid.NewGuid();
        var claimedOperationId = Guid.NewGuid();
        await Database.EnsureInitializedAsync(CancellationToken.None);
        await Database.ExecuteInTransactionAsync(
            async (connection, transaction, ct) =>
            {
                await Sut.UpsertPendingAsync(
                    connection,
                    transaction,
                    new OutboxOperationModel
                    {
                        Id = claimedOperationId,
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
        await using (var connection = await ConnectionFactory.OpenAsync(CancellationToken.None))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                UPDATE outboxoperations
                SET status = 'InProgress'
                WHERE id = $id AND userid = $userId;
                """;
            command.Parameters.AddWithValue("$id", claimedOperationId.ToString("D"));
            command.Parameters.AddWithValue("$userId", UserId.ToString("D"));
            (await command.ExecuteNonQueryAsync()).Should().Be(1);
        }

        // Act — later edit while claim is in progress
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

        // Completing revision 1 must not clear revision 2's pending intent
        await using (var connection = await ConnectionFactory.OpenAsync(CancellationToken.None))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                UPDATE outboxoperations
                SET status = 'Completed'
                WHERE id = $id AND userid = $userId AND status = 'InProgress';
                """;
            command.Parameters.AddWithValue("$id", claimedOperationId.ToString("D"));
            command.Parameters.AddWithValue("$userId", UserId.ToString("D"));
            (await command.ExecuteNonQueryAsync()).Should().Be(1);
        }

        var pending = await Sut.ListPendingForAggregateAsync(
            UserId,
            OutboxAggregateTypeEnum.Catch,
            aggregateId,
            CancellationToken.None);

        // Assert
        pending.Should().HaveCount(1);
        pending[0].Id.Should().NotBe(claimedOperationId);
        pending[0].PayloadJson.Should().Contain("\"revision\":2");
        pending[0].Status.Should().Be(OutboxStatusEnum.Pending);
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

    [Fact]
    public async Task ItShouldKeepTheLastErrorAndAttemptCountOfAPendingOperationWhenItIsCoalesced()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var first = DateTimeOffset.Parse("2026-06-01T12:00:00Z");
        var second = first.AddMinutes(5);
        await Database.EnsureInitializedAsync(CancellationToken.None);
        await UpsertAsync(aggregateId, "{\"revision\":1}", attemptCount: 2, lastError: "NotFound", availableAt: first.AddMinutes(30));

        // Act
        await UpsertAsync(aggregateId, "{\"revision\":2}", attemptCount: 0, lastError: null, availableAt: second);

        // Assert
        var pending = (await Sut.ListPendingForAggregateAsync(UserId, OutboxAggregateTypeEnum.Catch, aggregateId, CancellationToken.None)).Single();
        pending.PayloadJson.Should().Be("{\"revision\":2}");
        pending.AvailableAt.Should().Be(second);
        pending.LastError.Should().Be("NotFound");
        pending.AttemptCount.Should().Be(2);
    }

    private Task UpsertAsync(Guid aggregateId, string payload, int attemptCount, string? lastError, DateTimeOffset availableAt)
    {
        return Database.ExecuteInTransactionAsync(
            (connection, transaction, ct) => Sut.UpsertPendingAsync(
                connection,
                transaction,
                new OutboxOperationModel
                {
                    Id = Guid.NewGuid(),
                    UserId = UserId,
                    AggregateType = OutboxAggregateTypeEnum.Catch,
                    AggregateId = aggregateId,
                    OperationType = OutboxOperationTypeEnum.UpsertCatchMetadata,
                    PayloadJson = payload,
                    Status = OutboxStatusEnum.Pending,
                    AttemptCount = attemptCount,
                    LastError = lastError,
                    CreatedOn = DateTimeOffset.UtcNow,
                    AvailableAt = availableAt
                },
                ct),
            CancellationToken.None);
    }
}
