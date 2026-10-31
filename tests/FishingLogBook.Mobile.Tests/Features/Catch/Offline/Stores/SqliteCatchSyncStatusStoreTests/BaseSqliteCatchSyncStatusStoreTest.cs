using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using FishingLogBook.Mobile.Tests.Support;
using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Offline.Stores.SqliteCatchSyncStatusStoreTests;

public abstract class BaseSqliteCatchSyncStatusStoreTest : IDisposable
{
    protected static readonly Guid OwnerUserId = SqliteOfflineHarness.OwnerUserId;
    protected static readonly Guid OtherUserId = SqliteOfflineHarness.OtherUserId;
    protected static readonly DateTimeOffset Now = OutboxRowFixture.Now;

    protected readonly SqliteOfflineHarness Harness = new();
    protected readonly SqliteCatchSyncStatusStore Sut;
    protected readonly SqliteCatchStore OwnerCatchStore;

    protected BaseSqliteCatchSyncStatusStoreTest()
    {
        Sut = Harness.StatusStore;
        OwnerCatchStore = Harness.CreateCatchStore(OwnerUserId);
    }

    public void Dispose()
    {
        Harness.Dispose();
        GC.SuppressFinalize(this);
    }

    protected async Task<LocalCatchSaveArgs> SaveCatchAsync(
        int photographCount = 0,
        LocalCatchLifecycleEnum lifecycle = LocalCatchLifecycleEnum.Saved,
        SqliteCatchStore? store = null,
        LocalCatchSaveArgs? existing = null)
    {
        var args = existing ?? SqliteOfflineHarness.CreateSaveArgs(photographCount: photographCount, lifecycle: lifecycle);
        var result = await (store ?? OwnerCatchStore).SaveAsync(args, CancellationToken.None);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException("Arrange failed to save the catch.");
        }

        return args;
    }

    protected async Task<CatchSyncStatusModel> StatusOfAsync(Guid catchId, Guid? userId = null)
    {
        var statuses = await Sut.ListAsync(userId ?? OwnerUserId, CancellationToken.None);
        return statuses[catchId];
    }

    /// <summary>Overwrites the single saved metadata operation of the catch.</summary>
    protected async Task SetMetadataOperationAsync(
        Guid catchId,
        OutboxStatusEnum status,
        int attemptCount = 0,
        DateTimeOffset? availableAt = null,
        DateTimeOffset? createdOn = null)
    {
        await using var connection = await Harness.ConnectionFactory.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE outboxoperations
            SET status = $status, attemptcount = $attempts, availableat = $availableAt,
                createdon = COALESCE($createdOn, createdon)
            WHERE aggregateid = $catchId AND operationtype = 'UpsertCatchMetadata';
            """;
        command.Parameters.AddWithValue(
            "$createdOn",
            createdOn is null ? DBNull.Value : OutboxRowFixture.Format(createdOn.Value));
        command.Parameters.AddWithValue("$status", status.ToString());
        command.Parameters.AddWithValue("$attempts", attemptCount);
        command.Parameters.AddWithValue("$availableAt", OutboxRowFixture.Format(availableAt ?? Now));
        command.Parameters.AddWithValue("$catchId", catchId.ToString("D"));
        (await command.ExecuteNonQueryAsync()).Should().Be(1);
    }

    protected Task<Guid> AddPhotographOperationAsync(
        Guid photographId,
        OutboxStatusEnum status,
        int attemptCount = 0,
        DateTimeOffset? availableAt = null,
        DateTimeOffset? createdOn = null)
    {
        return OutboxRowFixture.InsertAsync(
            Harness.ConnectionFactory,
            OwnerUserId,
            photographId,
            status,
            OutboxOperationTypeEnum.UploadCatchPhotograph,
            OutboxAggregateTypeEnum.CatchPhotograph,
            attemptCount,
            createdOn ?? Now,
            availableAt ?? Now);
    }

    protected Task RecordPhotographAsync(Guid catchId, Guid photographId)
    {
        return Harness.StateStore.SetPhotographUploadedAsync(
            new PhotographSyncStateModel
            {
                UserId = OwnerUserId,
                CatchId = catchId,
                PhotographId = photographId,
                Status = PhotographSyncStatusEnum.Recorded,
                ObjectKey = "key"
            },
            Now,
            CancellationToken.None);
    }

    protected Task RecordMetadataAsync(Guid catchId, long revision)
    {
        return Harness.Database.ExecuteInTransactionAsync(
            (connection, transaction, ct) => Harness.StateStore.RecordSyncedRevisionAsync(
                connection,
                transaction,
                OwnerUserId,
                catchId,
                revision,
                Now,
                ct),
            CancellationToken.None);
    }
}
