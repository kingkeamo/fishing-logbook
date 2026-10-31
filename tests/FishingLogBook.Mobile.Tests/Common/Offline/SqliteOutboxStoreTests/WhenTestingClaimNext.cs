using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Enums;

namespace FishingLogBook.Mobile.Tests.Common.Offline.SqliteOutboxStoreTests;

public sealed class WhenTestingClaimNext : BaseSqliteOutboxLeaseTest
{
    [Fact]
    public async Task ItShouldRejectABlankOwner()
    {
        // Arrange
        await SeedAsync();

        // Act
        var act = async () => await Sut.ClaimNextAsync(UserId, " ", Now, LeaseDuration, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ItShouldReturnNullWhenNothingIsDue()
    {
        // Arrange
        await Database.EnsureInitializedAsync(CancellationToken.None);

        // Act
        var claimed = await Sut.ClaimNextAsync(UserId, OwnerA, Now, LeaseDuration, CancellationToken.None);

        // Assert
        claimed.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldSkipOperationsWhoseAvailableAtIsInTheFuture()
    {
        // Arrange
        var id = await SeedAsync(availableAt: Now.AddSeconds(1));

        // Act
        var claimed = await Sut.ClaimNextAsync(UserId, OwnerA, Now, LeaseDuration, CancellationToken.None);

        // Assert
        claimed.Should().BeNull();
        (await ReadAsync(id)).Status.Should().Be(OutboxStatusEnum.Pending);
    }

    [Fact]
    public async Task ItShouldClaimAnOperationWhoseAvailableAtIsExactlyNow()
    {
        // Arrange
        var id = await SeedAsync(availableAt: Now);

        // Act
        var claimed = await Sut.ClaimNextAsync(UserId, OwnerA, Now, LeaseDuration, CancellationToken.None);

        // Assert
        claimed.Should().NotBeNull();
        claimed!.Id.Should().Be(id);
    }

    [Fact]
    public async Task ItShouldNotClaimAnotherUsersOperations()
    {
        // Arrange
        var id = await SeedAsync(userId: OtherUserId);

        // Act
        var claimed = await Sut.ClaimNextAsync(UserId, OwnerA, Now, LeaseDuration, CancellationToken.None);

        // Assert
        claimed.Should().BeNull();
        (await ReadAsync(id)).Status.Should().Be(OutboxStatusEnum.Pending);
    }

    [Theory]
    [InlineData(OutboxStatusEnum.InProgress)]
    [InlineData(OutboxStatusEnum.Completed)]
    [InlineData(OutboxStatusEnum.Failed)]
    [InlineData(OutboxStatusEnum.Dead)]
    public async Task ItShouldNotClaimOperationsThatAreNotPending(OutboxStatusEnum status)
    {
        // Arrange
        var id = await SeedAsync(status: status);

        // Act
        var claimed = await Sut.ClaimNextAsync(UserId, OwnerA, Now, LeaseDuration, CancellationToken.None);

        // Assert
        claimed.Should().BeNull();
        (await ReadAsync(id)).Status.Should().Be(status);
    }

    [Fact]
    public async Task ItShouldClaimMetadataOperationsBeforeOlderPhotographUploads()
    {
        // Arrange
        var photoId = await SeedAsync(
            operationType: OutboxOperationTypeEnum.UploadCatchPhotograph,
            aggregateType: OutboxAggregateTypeEnum.CatchPhotograph,
            createdOn: Now.AddHours(-3));
        var metadataId = await SeedAsync(createdOn: Now.AddHours(-1));
        var deleteId = await SeedAsync(
            operationType: OutboxOperationTypeEnum.DeleteCatch,
            createdOn: Now.AddMinutes(-30));

        // Act
        var first = await Sut.ClaimNextAsync(UserId, OwnerA, Now, LeaseDuration, CancellationToken.None);
        var second = await Sut.ClaimNextAsync(UserId, OwnerA, Now, LeaseDuration, CancellationToken.None);
        var third = await Sut.ClaimNextAsync(UserId, OwnerA, Now, LeaseDuration, CancellationToken.None);

        // Assert
        first!.Id.Should().Be(metadataId);
        second!.Id.Should().Be(deleteId);
        third!.Id.Should().Be(photoId);
    }

    [Fact]
    public async Task ItShouldClaimTheOldestDueOperationFirstAndNeverTheSameRowTwice()
    {
        // Arrange
        var newest = await SeedAsync(createdOn: Now.AddMinutes(-1));
        var oldest = await SeedAsync(createdOn: Now.AddMinutes(-20));
        var middle = await SeedAsync(createdOn: Now.AddMinutes(-10));

        // Act
        var first = await Sut.ClaimNextAsync(UserId, OwnerA, Now, LeaseDuration, CancellationToken.None);
        var second = await Sut.ClaimNextAsync(UserId, OwnerA, Now, LeaseDuration, CancellationToken.None);
        var third = await Sut.ClaimNextAsync(UserId, OwnerA, Now, LeaseDuration, CancellationToken.None);
        var fourth = await Sut.ClaimNextAsync(UserId, OwnerA, Now, LeaseDuration, CancellationToken.None);

        // Assert
        new[] { first!.Id, second!.Id, third!.Id }.Should().Equal(oldest, middle, newest);
        fourth.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldMarkTheOperationInProgressWithALeaseAndAnIncrementedAttemptCount()
    {
        // Arrange
        var id = await SeedAsync(attemptCount: 2);

        // Act
        var claimed = await Sut.ClaimNextAsync(UserId, OwnerA, Now, LeaseDuration, CancellationToken.None);

        // Assert
        var row = await ReadAsync(id);
        row.Status.Should().Be(OutboxStatusEnum.InProgress);
        row.AttemptCount.Should().Be(3);
        row.LeaseOwner.Should().Be(OwnerA);
        row.LeaseExpiresAt.Should().Be(Now.Add(LeaseDuration));
        row.LastAttemptAt.Should().Be(Now);
        claimed!.Status.Should().Be(OutboxStatusEnum.InProgress);
        claimed.AttemptCount.Should().Be(3);
        claimed.LeaseOwner.Should().Be(OwnerA);
        claimed.LeaseExpiresAt.Should().Be(Now.Add(LeaseDuration));
        claimed.LastAttemptAt.Should().Be(Now);
        claimed.UserId.Should().Be(UserId);
    }
}
