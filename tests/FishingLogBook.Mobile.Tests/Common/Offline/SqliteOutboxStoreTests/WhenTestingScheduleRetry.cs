using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Enums;

namespace FishingLogBook.Mobile.Tests.Common.Offline.SqliteOutboxStoreTests;

public sealed class WhenTestingScheduleRetry : BaseSqliteOutboxLeaseTest
{
    [Fact]
    public async Task ItShouldRejectANullClaimedOperation()
    {
        // Arrange
        await Database.EnsureInitializedAsync(CancellationToken.None);

        // Act
        var act = async () => await Sut.ScheduleRetryAsync(null!, Now, "Timeout", CancellationToken.None);

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
        await Sut.ScheduleRetryAsync(claimed, Now.AddHours(1), "Timeout", CancellationToken.None);

        // Assert
        (await ReadAsync(claimed.Id)).Should().Be(before);
    }

    [Fact]
    public async Task ItShouldSupersedeTheClaimedOperationWhenANewerPendingSiblingExists()
    {
        // Arrange
        var claimed = await SeedAndClaimAsync();
        var newer = await SeedAsync(aggregateId: claimed.AggregateId, createdOn: Now);

        // Act
        await Sut.ScheduleRetryAsync(claimed, Now.AddHours(1), "Timeout", CancellationToken.None);

        // Assert
        var row = await ReadAsync(claimed.Id);
        row.Status.Should().Be(OutboxStatusEnum.Completed);
        row.LastError.Should().Be("Superseded");
        (await ReadAsync(newer)).AvailableAt.Should().NotBe(Now.AddHours(1));
    }

    [Fact]
    public async Task ItShouldReturnTheOperationToPendingAtTheScheduledTimeKeepingTheAttemptCount()
    {
        // Arrange
        var claimed = await SeedAndClaimAsync(attemptCount: 1);
        var retryAt = Now.AddMinutes(15);

        // Act
        await Sut.ScheduleRetryAsync(claimed, retryAt, "Timeout", CancellationToken.None);

        // Assert
        var row = await ReadAsync(claimed.Id);
        row.Status.Should().Be(OutboxStatusEnum.Pending);
        row.AvailableAt.Should().Be(retryAt);
        row.LastError.Should().Be("Timeout");
        row.AttemptCount.Should().Be(2);
        row.LeaseOwner.Should().BeNull();
        row.LeaseExpiresAt.Should().BeNull();
        (await Sut.ClaimNextAsync(UserId, OwnerA, Now, LeaseDuration, CancellationToken.None)).Should().BeNull();
        (await Sut.ClaimNextAsync(UserId, OwnerA, retryAt, LeaseDuration, CancellationToken.None))!
            .Id.Should().Be(claimed.Id);
    }
}
