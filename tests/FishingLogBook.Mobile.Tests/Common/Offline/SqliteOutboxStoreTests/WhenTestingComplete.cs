using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Models;

namespace FishingLogBook.Mobile.Tests.Common.Offline.SqliteOutboxStoreTests;

public sealed class WhenTestingComplete : BaseSqliteOutboxLeaseTest
{
    [Fact]
    public async Task ItShouldRejectAClaimedOperationWhoseLeaseIsHeldByADifferentOwner()
    {
        // Arrange
        var claimed = await SeedAndClaimAsync(OwnerA);
        var impostor = CopyWithOwner(claimed, OwnerB);

        // Act
        var act = async () => await Database.ExecuteInTransactionAsync(
            (connection, transaction, ct) => Sut.CompleteAsync(connection, transaction, impostor, Now, ct),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        var row = await ReadAsync(claimed.Id);
        row.Status.Should().Be(OutboxStatusEnum.InProgress);
        row.LeaseOwner.Should().Be(OwnerA);
    }

    [Fact]
    public async Task ItShouldRejectAnOperationThatIsNotInProgress()
    {
        // Arrange
        var claimed = await SeedAndClaimAsync();
        await Sut.ReleaseAsync(claimed, CancellationToken.None);

        // Act
        var act = async () => await Database.ExecuteInTransactionAsync(
            (connection, transaction, ct) => Sut.CompleteAsync(connection, transaction, claimed, Now, ct),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await ReadAsync(claimed.Id)).Status.Should().Be(OutboxStatusEnum.Pending);
    }

    [Fact]
    public async Task ItShouldRejectAnOperationWhoseLeaseWasRecoveredByAnotherProcess()
    {
        // Arrange
        var claimed = await SeedAndClaimAsync(OwnerA);
        await Sut.RecoverExpiredLeasesAsync(UserId, OwnerB, Now, CancellationToken.None);

        // Act
        var act = async () => await Database.ExecuteInTransactionAsync(
            (connection, transaction, ct) => Sut.CompleteAsync(connection, transaction, claimed, Now, ct),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await ReadAsync(claimed.Id)).Status.Should().Be(OutboxStatusEnum.Pending);
    }

    [Fact]
    public async Task ItShouldRejectANullClaimedOperation()
    {
        // Arrange
        await Database.EnsureInitializedAsync(CancellationToken.None);

        // Act
        var act = async () => await Database.ExecuteInTransactionAsync(
            (connection, transaction, ct) => Sut.CompleteAsync(connection, transaction, null!, Now, ct),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ItShouldLeaveTheOperationInProgressWhenTheCallersTransactionRollsBack()
    {
        // Arrange
        var claimed = await SeedAndClaimAsync();

        // Act
        var act = async () => await Database.ExecuteInTransactionAsync(
            async (connection, transaction, ct) =>
            {
                await Sut.CompleteAsync(connection, transaction, claimed, Now, ct);
                throw new InvalidOperationException("simulated failure after completion");
            },
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        var row = await ReadAsync(claimed.Id);
        row.Status.Should().Be(OutboxStatusEnum.InProgress);
        row.LeaseOwner.Should().Be(OwnerA);
    }

    [Fact]
    public async Task ItShouldMarkTheOperationCompletedAndClearTheLeaseWhenTheTransactionCommits()
    {
        // Arrange
        var claimed = await SeedAndClaimAsync();
        var completedAt = Now.AddSeconds(30);

        // Act
        await Database.ExecuteInTransactionAsync(
            (connection, transaction, ct) => Sut.CompleteAsync(connection, transaction, claimed, completedAt, ct),
            CancellationToken.None);

        // Assert
        var row = await ReadAsync(claimed.Id);
        row.Status.Should().Be(OutboxStatusEnum.Completed);
        row.LeaseOwner.Should().BeNull();
        row.LeaseExpiresAt.Should().BeNull();
        row.LastError.Should().BeNull();
        row.LastAttemptAt.Should().Be(completedAt);
        (await Sut.ListPendingAsync(UserId, CancellationToken.None)).Should().BeEmpty();
    }

    private static OutboxOperationModel CopyWithOwner(OutboxOperationModel source, string owner)
    {
        return new OutboxOperationModel
        {
            Id = source.Id,
            UserId = source.UserId,
            AggregateType = source.AggregateType,
            AggregateId = source.AggregateId,
            OperationType = source.OperationType,
            PayloadJson = source.PayloadJson,
            Status = source.Status,
            AttemptCount = source.AttemptCount,
            CreatedOn = source.CreatedOn,
            AvailableAt = source.AvailableAt,
            LeaseOwner = owner,
            LeaseExpiresAt = source.LeaseExpiresAt
        };
    }
}
