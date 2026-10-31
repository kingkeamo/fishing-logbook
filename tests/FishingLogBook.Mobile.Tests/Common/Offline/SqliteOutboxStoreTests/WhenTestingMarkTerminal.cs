using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Enums;

namespace FishingLogBook.Mobile.Tests.Common.Offline.SqliteOutboxStoreTests;

public sealed class WhenTestingMarkTerminal : BaseSqliteOutboxLeaseTest
{
    [Theory]
    [InlineData(OutboxStatusEnum.Pending)]
    [InlineData(OutboxStatusEnum.InProgress)]
    [InlineData(OutboxStatusEnum.Completed)]
    public async Task ItShouldRejectStatusesThatAreNotTerminalFailures(OutboxStatusEnum status)
    {
        // Arrange
        var claimed = await SeedAndClaimAsync();

        // Act
        var act = async () => await Sut.MarkTerminalAsync(claimed, status, "Bad", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        (await ReadAsync(claimed.Id)).Status.Should().Be(OutboxStatusEnum.InProgress);
    }

    [Fact]
    public async Task ItShouldRejectANullClaimedOperation()
    {
        // Arrange
        await Database.EnsureInitializedAsync(CancellationToken.None);

        // Act
        var act = async () => await Sut.MarkTerminalAsync(null!, OutboxStatusEnum.Failed, "Bad", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ItShouldNotChangeAnOperationThatIsNoLongerLeasedByTheClaimant()
    {
        // Arrange
        var claimed = await SeedAndClaimAsync(OwnerA);
        await Sut.RecoverExpiredLeasesAsync(UserId, OwnerB, Now, CancellationToken.None);
        var before = await ReadAsync(claimed.Id);

        // Act
        await Sut.MarkTerminalAsync(claimed, OutboxStatusEnum.Failed, "Rejected", CancellationToken.None);

        // Assert
        (await ReadAsync(claimed.Id)).Should().Be(before);
    }

    [Fact]
    public async Task ItShouldSupersedeTheClaimedOperationWhenANewerPendingSiblingExists()
    {
        // Arrange
        var claimed = await SeedAndClaimAsync();
        await SeedAsync(aggregateId: claimed.AggregateId, createdOn: Now);

        // Act
        await Sut.MarkTerminalAsync(claimed, OutboxStatusEnum.Failed, "Rejected", CancellationToken.None);

        // Assert
        var row = await ReadAsync(claimed.Id);
        row.Status.Should().Be(OutboxStatusEnum.Completed);
        row.LastError.Should().Be("Superseded");
    }

    [Theory]
    [InlineData(OutboxStatusEnum.Failed)]
    [InlineData(OutboxStatusEnum.Dead)]
    public async Task ItShouldStoreTheTerminalStatusAndErrorAndClearTheLeaseWithoutChangingAvailableAt(
        OutboxStatusEnum status)
    {
        // Arrange
        var claimed = await SeedAndClaimAsync(attemptCount: 1);
        var availableAtBefore = (await ReadAsync(claimed.Id)).AvailableAt;

        // Act
        await Sut.MarkTerminalAsync(claimed, status, "Rejected", CancellationToken.None);

        // Assert
        var row = await ReadAsync(claimed.Id);
        row.Status.Should().Be(status);
        row.LastError.Should().Be("Rejected");
        row.AvailableAt.Should().Be(availableAtBefore);
        row.AttemptCount.Should().Be(2);
        row.LeaseOwner.Should().BeNull();
        row.LeaseExpiresAt.Should().BeNull();
        (await Sut.ClaimNextAsync(UserId, OwnerA, Now.AddDays(1), LeaseDuration, CancellationToken.None))
            .Should().BeNull();
    }
}
