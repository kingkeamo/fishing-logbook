using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Enums;

namespace FishingLogBook.Mobile.Tests.Common.Offline.SqliteOutboxStoreTests;

public sealed class WhenTestingGetNextDue : BaseSqliteOutboxLeaseTest
{
    [Fact]
    public async Task ItShouldReturnNullWhenThereAreNoPendingOperations()
    {
        // Arrange
        await SeedAsync(status: OutboxStatusEnum.Completed);
        await SeedAsync(status: OutboxStatusEnum.InProgress);
        await SeedAsync(status: OutboxStatusEnum.Failed);

        // Act
        var next = await Sut.GetNextDueAsync(UserId, CancellationToken.None);

        // Assert
        next.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldIgnoreOtherUsersPendingOperations()
    {
        // Arrange
        await SeedAsync(userId: OtherUserId, availableAt: Now.AddMinutes(-90));
        var own = await SeedAsync(availableAt: Now.AddMinutes(5));

        // Act
        var next = await Sut.GetNextDueAsync(UserId, CancellationToken.None);

        // Assert
        next.Should().Be(Now.AddMinutes(5));
        (await ReadAsync(own)).Status.Should().Be(OutboxStatusEnum.Pending);
    }

    [Fact]
    public async Task ItShouldIgnoreOperationsThatAreNotPending()
    {
        // Arrange
        await SeedAsync(status: OutboxStatusEnum.Failed, availableAt: Now.AddMinutes(-90));
        await SeedAsync(status: OutboxStatusEnum.InProgress, availableAt: Now.AddMinutes(-80));
        await SeedAsync(availableAt: Now.AddMinutes(10));

        // Act
        var next = await Sut.GetNextDueAsync(UserId, CancellationToken.None);

        // Assert
        next.Should().Be(Now.AddMinutes(10));
    }

    [Fact]
    public async Task ItShouldReturnTheEarliestAvailableAtOfThePendingOperations()
    {
        // Arrange
        await SeedAsync(availableAt: Now.AddMinutes(30));
        await SeedAsync(availableAt: Now.AddMinutes(-5));
        await SeedAsync(availableAt: Now.AddMinutes(2));

        // Act
        var next = await Sut.GetNextDueAsync(UserId, CancellationToken.None);

        // Assert
        next.Should().Be(Now.AddMinutes(-5));
    }
}
