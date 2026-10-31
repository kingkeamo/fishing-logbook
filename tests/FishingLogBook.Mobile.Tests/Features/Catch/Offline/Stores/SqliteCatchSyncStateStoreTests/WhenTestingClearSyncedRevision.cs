using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Offline.Stores.SqliteCatchSyncStateStoreTests;

public sealed class WhenTestingClearSyncedRevision : BaseSqliteCatchSyncStateStoreTest
{
    [Fact]
    public async Task ItShouldDoNothingWhenNoRevisionWasRecorded()
    {
        // Arrange
        var args = await SaveCatchAsync();

        // Act
        await Sut.ClearSyncedRevisionAsync(OwnerUserId, args.Id, CancellationToken.None);

        // Assert
        (await Sut.GetSyncedRevisionAsync(OwnerUserId, args.Id, CancellationToken.None)).Should().Be(0);
    }

    [Fact]
    public async Task ItShouldNotClearARevisionRecordedForAnotherUser()
    {
        // Arrange
        var args = await SaveCatchAsync();
        await RecordRevisionAsync(OwnerUserId, args.Id, 2);

        // Act
        await Sut.ClearSyncedRevisionAsync(OtherUserId, args.Id, CancellationToken.None);

        // Assert
        (await Sut.GetSyncedRevisionAsync(OwnerUserId, args.Id, CancellationToken.None)).Should().Be(2);
    }

    [Fact]
    public async Task ItShouldRemoveTheRecordedRevision()
    {
        // Arrange
        var args = await SaveCatchAsync();
        await RecordRevisionAsync(OwnerUserId, args.Id, 2);

        // Act
        await Sut.ClearSyncedRevisionAsync(OwnerUserId, args.Id, CancellationToken.None);

        // Assert
        (await Sut.GetSyncedRevisionAsync(OwnerUserId, args.Id, CancellationToken.None)).Should().Be(0);
    }
}
