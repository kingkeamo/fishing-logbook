using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Offline.Stores.SqliteCatchSyncStateStoreTests;

public sealed class WhenTestingGetSyncedRevision : BaseSqliteCatchSyncStateStoreTest
{
    [Fact]
    public async Task ItShouldReturnZeroWhenNothingHasBeenRecorded()
    {
        // Arrange
        var args = await SaveCatchAsync();

        // Act
        var revision = await Sut.GetSyncedRevisionAsync(OwnerUserId, args.Id, CancellationToken.None);

        // Assert
        revision.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldReturnZeroForAnotherUsersRecordedRevision()
    {
        // Arrange
        var args = await SaveCatchAsync();
        await RecordRevisionAsync(OwnerUserId, args.Id, 2);

        // Act
        var revision = await Sut.GetSyncedRevisionAsync(OtherUserId, args.Id, CancellationToken.None);

        // Assert
        revision.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldReturnTheRecordedRevision()
    {
        // Arrange
        var args = await SaveCatchAsync();
        await RecordRevisionAsync(OwnerUserId, args.Id, 2);

        // Act
        var revision = await Sut.GetSyncedRevisionAsync(OwnerUserId, args.Id, CancellationToken.None);

        // Assert
        revision.Should().Be(2);
    }
}
