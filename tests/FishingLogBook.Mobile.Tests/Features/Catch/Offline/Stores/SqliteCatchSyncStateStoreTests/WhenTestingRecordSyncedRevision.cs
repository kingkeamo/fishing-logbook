using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Offline.Stores.SqliteCatchSyncStateStoreTests;

public sealed class WhenTestingRecordSyncedRevision : BaseSqliteCatchSyncStateStoreTest
{
    [Fact]
    public async Task ItShouldNotOverwriteAnotherUsersRowForTheSameCatchId()
    {
        // Arrange
        var args = await SaveCatchAsync();
        await RecordRevisionAsync(OwnerUserId, args.Id, 3);

        // Act
        await RecordRevisionAsync(OtherUserId, args.Id, 9);

        // Assert
        (await Sut.GetSyncedRevisionAsync(OwnerUserId, args.Id, CancellationToken.None)).Should().Be(3);
        (await Sut.GetSyncedRevisionAsync(OtherUserId, args.Id, CancellationToken.None)).Should().Be(0);
    }

    [Fact]
    public async Task ItShouldKeepTheHighestRevisionWhenALowerRevisionIsRecordedLater()
    {
        // Arrange
        var args = await SaveCatchAsync();
        await RecordRevisionAsync(OwnerUserId, args.Id, 3);

        // Act
        await RecordRevisionAsync(OwnerUserId, args.Id, 2);

        // Assert
        (await Sut.GetSyncedRevisionAsync(OwnerUserId, args.Id, CancellationToken.None)).Should().Be(3);
    }

    [Fact]
    public async Task ItShouldRollBackWhenTheCallersTransactionFails()
    {
        // Arrange
        var args = await SaveCatchAsync();

        // Act
        var act = async () => await Harness.Database.ExecuteInTransactionAsync(
            async (connection, transaction, ct) =>
            {
                await Sut.RecordSyncedRevisionAsync(connection, transaction, OwnerUserId, args.Id, 1, Now, ct);
                throw new InvalidOperationException("simulated failure");
            },
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await Sut.GetSyncedRevisionAsync(OwnerUserId, args.Id, CancellationToken.None)).Should().Be(0);
    }

    [Fact]
    public async Task ItShouldSurviveASubsequentSaveOfTheSameCatch()
    {
        // Arrange
        var args = await SaveCatchAsync();
        await RecordRevisionAsync(OwnerUserId, args.Id, 1);

        // Act
        var resave = await OwnerCatchStore.SaveAsync(args, CancellationToken.None);

        // Assert
        resave.Revision.Should().Be(2);
        (await Sut.GetSyncedRevisionAsync(OwnerUserId, args.Id, CancellationToken.None)).Should().Be(1);
    }

    [Fact]
    public async Task ItShouldRaiseTheRecordedRevisionAndPersistIt()
    {
        // Arrange
        var args = await SaveCatchAsync();
        await RecordRevisionAsync(OwnerUserId, args.Id, 1);

        // Act
        await RecordRevisionAsync(OwnerUserId, args.Id, 4);

        // Assert
        (await Sut.GetSyncedRevisionAsync(OwnerUserId, args.Id, CancellationToken.None)).Should().Be(4);
    }
}
