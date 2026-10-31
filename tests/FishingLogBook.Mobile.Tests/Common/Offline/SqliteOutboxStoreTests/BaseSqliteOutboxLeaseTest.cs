using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Models;
using FishingLogBook.Mobile.Tests.Support;

namespace FishingLogBook.Mobile.Tests.Common.Offline.SqliteOutboxStoreTests;

public abstract class BaseSqliteOutboxLeaseTest : BaseSqliteOutboxStoreTest
{
    protected const string OwnerA = "owner-a";
    protected const string OwnerB = "owner-b";

    protected static readonly Guid OtherUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    protected static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    protected static readonly DateTimeOffset Now = OutboxRowFixture.Now;

    protected async Task<Guid> SeedAsync(
        Guid? userId = null,
        Guid? aggregateId = null,
        OutboxStatusEnum status = OutboxStatusEnum.Pending,
        OutboxOperationTypeEnum operationType = OutboxOperationTypeEnum.UpsertCatchMetadata,
        OutboxAggregateTypeEnum aggregateType = OutboxAggregateTypeEnum.Catch,
        int attemptCount = 0,
        DateTimeOffset? createdOn = null,
        DateTimeOffset? availableAt = null,
        string? leaseOwner = null,
        DateTimeOffset? leaseExpiresAt = null)
    {
        await Database.EnsureInitializedAsync(CancellationToken.None);
        return await OutboxRowFixture.InsertAsync(
            ConnectionFactory,
            userId ?? UserId,
            aggregateId ?? Guid.NewGuid(),
            status,
            operationType,
            aggregateType,
            attemptCount,
            createdOn,
            availableAt,
            leaseOwner,
            leaseExpiresAt);
    }

    protected async Task<OutboxRowSnapshot> ReadAsync(Guid id)
    {
        await Database.EnsureInitializedAsync(CancellationToken.None);
        return await OutboxRowFixture.GetAsync(ConnectionFactory, id);
    }

    /// <summary>Seeds a single Pending row and claims it, returning the claimed model.</summary>
    protected async Task<OutboxOperationModel> SeedAndClaimAsync(
        string owner = OwnerA,
        Guid? aggregateId = null,
        int attemptCount = 0,
        OutboxOperationTypeEnum operationType = OutboxOperationTypeEnum.UpsertCatchMetadata)
    {
        var id = await SeedAsync(
            aggregateId: aggregateId,
            attemptCount: attemptCount,
            operationType: operationType,
            aggregateType: operationType == OutboxOperationTypeEnum.UploadCatchPhotograph
                ? OutboxAggregateTypeEnum.CatchPhotograph
                : OutboxAggregateTypeEnum.Catch);
        var claimed = await Sut.ClaimNextAsync(UserId, owner, Now, LeaseDuration, CancellationToken.None);
        if (claimed is null || claimed.Id != id)
        {
            throw new InvalidOperationException("Arrange failed to claim the seeded operation.");
        }

        return claimed;
    }
}
