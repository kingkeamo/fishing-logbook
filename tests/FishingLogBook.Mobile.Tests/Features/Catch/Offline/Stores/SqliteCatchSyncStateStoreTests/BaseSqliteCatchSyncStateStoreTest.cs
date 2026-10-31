using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using FishingLogBook.Mobile.Tests.Support;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Offline.Stores.SqliteCatchSyncStateStoreTests;

public abstract class BaseSqliteCatchSyncStateStoreTest : IDisposable
{
    protected static readonly Guid OwnerUserId = SqliteOfflineHarness.OwnerUserId;
    protected static readonly Guid OtherUserId = SqliteOfflineHarness.OtherUserId;
    protected static readonly DateTimeOffset Now = OutboxRowFixture.Now;

    protected readonly SqliteOfflineHarness Harness = new();
    protected readonly SqliteCatchSyncStateStore Sut;
    protected readonly SqliteCatchStore OwnerCatchStore;

    protected BaseSqliteCatchSyncStateStoreTest()
    {
        Sut = Harness.StateStore;
        OwnerCatchStore = Harness.CreateCatchStore(OwnerUserId);
    }

    public void Dispose()
    {
        Harness.Dispose();
        GC.SuppressFinalize(this);
    }

    protected async Task<LocalCatchSaveArgs> SaveCatchAsync(int photographCount = 1)
    {
        var args = SqliteOfflineHarness.CreateSaveArgs(photographCount: photographCount);
        var result = await OwnerCatchStore.SaveAsync(args, CancellationToken.None);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException("Arrange failed to save the catch.");
        }

        return args;
    }

    protected Task RecordRevisionAsync(Guid userId, Guid catchId, long revision)
    {
        return Harness.Database.ExecuteInTransactionAsync(
            (connection, transaction, ct) => Sut.RecordSyncedRevisionAsync(
                connection,
                transaction,
                userId,
                catchId,
                revision,
                Now,
                ct),
            CancellationToken.None);
    }

    protected static PhotographSyncStateModel State(
        Guid userId,
        Guid catchId,
        Guid photographId,
        PhotographSyncStatusEnum status,
        string? objectKey = null)
    {
        return new PhotographSyncStateModel
        {
            UserId = userId,
            CatchId = catchId,
            PhotographId = photographId,
            Status = status,
            ObjectKey = objectKey
        };
    }
}
