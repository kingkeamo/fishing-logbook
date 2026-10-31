using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Enums;

namespace FishingLogBook.Mobile.Tests.Common.Offline.SqliteOutboxStoreTests;

public sealed class WhenTestingRecoverExpiredLeases : BaseSqliteOutboxLeaseTest
{
    [Fact]
    public async Task ItShouldRejectABlankCurrentOwner()
    {
        // Arrange
        await Database.EnsureInitializedAsync(CancellationToken.None);

        // Act
        var act = async () => await Sut.RecoverExpiredLeasesAsync(UserId, "", Now, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ItShouldNotRecoverALeaseOwnedByTheCurrentOwnerThatHasNotExpired()
    {
        // Arrange
        var id = await SeedAsync(
            status: OutboxStatusEnum.InProgress,
            leaseOwner: OwnerA,
            leaseExpiresAt: Now.AddSeconds(1));

        // Act
        var recovered = await Sut.RecoverExpiredLeasesAsync(UserId, OwnerA, Now, CancellationToken.None);

        // Assert
        recovered.Should().Be(0);
        var row = await ReadAsync(id);
        row.Status.Should().Be(OutboxStatusEnum.InProgress);
        row.LeaseOwner.Should().Be(OwnerA);
    }

    [Fact]
    public async Task ItShouldNotTouchPendingOrTerminalOperations()
    {
        // Arrange
        var pending = await SeedAsync();
        var completed = await SeedAsync(status: OutboxStatusEnum.Completed);
        var failed = await SeedAsync(status: OutboxStatusEnum.Failed);

        // Act
        var recovered = await Sut.RecoverExpiredLeasesAsync(UserId, OwnerA, Now, CancellationToken.None);

        // Assert
        recovered.Should().Be(0);
        (await ReadAsync(pending)).Status.Should().Be(OutboxStatusEnum.Pending);
        (await ReadAsync(completed)).Status.Should().Be(OutboxStatusEnum.Completed);
        (await ReadAsync(failed)).Status.Should().Be(OutboxStatusEnum.Failed);
    }

    [Fact]
    public async Task ItShouldNotRecoverAnotherUsersExpiredLeases()
    {
        // Arrange
        var id = await SeedAsync(
            userId: OtherUserId,
            status: OutboxStatusEnum.InProgress,
            leaseOwner: OwnerB,
            leaseExpiresAt: Now.AddHours(-1));

        // Act
        var recovered = await Sut.RecoverExpiredLeasesAsync(UserId, OwnerA, Now, CancellationToken.None);

        // Assert
        recovered.Should().Be(0);
        (await ReadAsync(id)).Status.Should().Be(OutboxStatusEnum.InProgress);
    }

    [Fact]
    public async Task ItShouldReturnExpiredOperationsOwnedByTheCurrentOwnerToPendingWithALeaseExpiredError()
    {
        // Arrange
        var id = await SeedAsync(
            status: OutboxStatusEnum.InProgress,
            attemptCount: 1,
            leaseOwner: OwnerA,
            leaseExpiresAt: Now);

        // Act
        var recovered = await Sut.RecoverExpiredLeasesAsync(UserId, OwnerA, Now, CancellationToken.None);

        // Assert
        recovered.Should().Be(1);
        var row = await ReadAsync(id);
        row.Status.Should().Be(OutboxStatusEnum.Pending);
        row.LastError.Should().Be("LeaseExpired");
        row.LeaseOwner.Should().BeNull();
        row.LeaseExpiresAt.Should().BeNull();
        row.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldImmediatelyRecoverAnUnexpiredLeaseOwnedByADifferentOwnerAfterARestart()
    {
        // Arrange
        var id = await SeedAsync(
            status: OutboxStatusEnum.InProgress,
            leaseOwner: OwnerB,
            leaseExpiresAt: Now.AddMinutes(4));

        // Act
        var recovered = await Sut.RecoverExpiredLeasesAsync(UserId, OwnerA, Now, CancellationToken.None);

        // Assert
        recovered.Should().Be(1);
        var row = await ReadAsync(id);
        row.Status.Should().Be(OutboxStatusEnum.Pending);
        row.LastError.Should().Be("LeaseExpired");
        row.LeaseOwner.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldRecoverAnInProgressRowThatHasNoLease()
    {
        // Arrange
        var id = await SeedAsync(status: OutboxStatusEnum.InProgress);

        // Act
        var recovered = await Sut.RecoverExpiredLeasesAsync(UserId, OwnerA, Now, CancellationToken.None);

        // Assert
        recovered.Should().Be(1);
        (await ReadAsync(id)).Status.Should().Be(OutboxStatusEnum.Pending);
    }

    [Fact]
    public async Task ItShouldSupersedeAnExpiredOperationThatHasANewerPendingSiblingWithoutViolatingTheUniqueIndex()
    {
        // Arrange
        var aggregateId = Guid.NewGuid();
        var stale = await SeedAsync(
            aggregateId: aggregateId,
            status: OutboxStatusEnum.InProgress,
            leaseOwner: OwnerB,
            leaseExpiresAt: Now.AddHours(-1),
            createdOn: Now.AddMinutes(-20));
        var newer = await SeedAsync(aggregateId: aggregateId, createdOn: Now.AddMinutes(-5));
        var otherAggregate = await SeedAsync(
            status: OutboxStatusEnum.InProgress,
            leaseOwner: OwnerB,
            leaseExpiresAt: Now.AddHours(-1));

        // Act
        var recovered = await Sut.RecoverExpiredLeasesAsync(UserId, OwnerA, Now, CancellationToken.None);

        // Assert
        recovered.Should().Be(2);
        var staleRow = await ReadAsync(stale);
        staleRow.Status.Should().Be(OutboxStatusEnum.Completed);
        staleRow.LastError.Should().Be("Superseded");
        staleRow.LeaseOwner.Should().BeNull();
        (await ReadAsync(newer)).Status.Should().Be(OutboxStatusEnum.Pending);
        var other = await ReadAsync(otherAggregate);
        other.Status.Should().Be(OutboxStatusEnum.Pending);
        other.LastError.Should().Be("LeaseExpired");
    }
}
