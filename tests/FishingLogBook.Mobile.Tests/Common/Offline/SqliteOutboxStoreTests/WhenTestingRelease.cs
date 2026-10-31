using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Enums;

namespace FishingLogBook.Mobile.Tests.Common.Offline.SqliteOutboxStoreTests;

public sealed class WhenTestingRelease : BaseSqliteOutboxLeaseTest
{
    [Fact]
    public async Task ItShouldRejectANullClaimedOperation()
    {
        // Arrange
        await Database.EnsureInitializedAsync(CancellationToken.None);

        // Act
        var act = async () => await Sut.ReleaseAsync(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ItShouldNotChangeAnOperationThatIsNoLongerLeasedByTheClaimant()
    {
        // Arrange
        var claimed = await SeedAndClaimAsync(OwnerA, attemptCount: 1);
        await Sut.RecoverExpiredLeasesAsync(UserId, OwnerB, Now, CancellationToken.None);
        var before = await ReadAsync(claimed.Id);

        // Act
        await Sut.ReleaseAsync(claimed, CancellationToken.None);

        // Assert
        var after = await ReadAsync(claimed.Id);
        after.Should().Be(before);
        after.AttemptCount.Should().Be(2);
    }

    [Fact]
    public async Task ItShouldNeverDecrementTheAttemptCountBelowZero()
    {
        // Arrange
        var claimed = await SeedAndClaimAsync();
        await ForceAttemptCountAsync(claimed.Id, 0);

        // Act
        await Sut.ReleaseAsync(claimed, CancellationToken.None);

        // Assert
        (await ReadAsync(claimed.Id)).AttemptCount.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldSupersedeTheClaimedOperationWhenANewerPendingSiblingExists()
    {
        // Arrange
        var claimed = await SeedAndClaimAsync();
        var newer = await SeedAsync(aggregateId: claimed.AggregateId, createdOn: Now);

        // Act
        await Sut.ReleaseAsync(claimed, CancellationToken.None);

        // Assert
        var row = await ReadAsync(claimed.Id);
        row.Status.Should().Be(OutboxStatusEnum.Completed);
        row.LastError.Should().Be("Superseded");
        row.LeaseOwner.Should().BeNull();
        (await ReadAsync(newer)).Status.Should().Be(OutboxStatusEnum.Pending);
    }

    [Fact]
    public async Task ItShouldReturnTheOperationToPendingWithoutConsumingAnAttemptAndKeepAvailableAtUnchanged()
    {
        // Arrange
        var claimed = await SeedAndClaimAsync(attemptCount: 2);
        var availableAtBefore = (await ReadAsync(claimed.Id)).AvailableAt;

        // Act
        await Sut.ReleaseAsync(claimed, CancellationToken.None);

        // Assert
        var row = await ReadAsync(claimed.Id);
        row.Status.Should().Be(OutboxStatusEnum.Pending);
        row.AttemptCount.Should().Be(2);
        row.AvailableAt.Should().Be(availableAtBefore);
        row.LeaseOwner.Should().BeNull();
        row.LeaseExpiresAt.Should().BeNull();
        row.LastError.Should().BeNull();
    }

    private async Task ForceAttemptCountAsync(Guid id, int attemptCount)
    {
        await using var connection = await ConnectionFactory.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE outboxoperations SET attemptcount = $count WHERE id = $id;";
        command.Parameters.AddWithValue("$count", attemptCount);
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await command.ExecuteNonQueryAsync();
    }
}
