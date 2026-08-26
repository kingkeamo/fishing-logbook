using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.Stores.CatchStoreTests;

public class WhenTestingCleanup : BaseCatchStoreTest
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-26T12:00:00Z");

    [Fact]
    public async Task ItShouldRemoveASyncedCatchOlderThanTheCutoff()
    {
        // Arrange
        await SaveSyncedCatchAsync(Guid.NewGuid(), OwnerUserId, syncedAt: Now.AddHours(-25));

        // Act
        var removed = await Sut.CleanupSyncedCacheAsync(OwnerUserId, Now.AddHours(-24), CancellationToken.None);

        // Assert
        removed.Should().Be(1);
        BackingCatches.Should().BeEmpty();
        BackingPhotographs.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRetainASyncedCatchNewerThanTheCutoff()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        await SaveSyncedCatchAsync(catchId, OwnerUserId, syncedAt: Now.AddHours(-23));

        // Act
        var removed = await Sut.CleanupSyncedCacheAsync(OwnerUserId, Now.AddHours(-24), CancellationToken.None);

        // Assert
        removed.Should().Be(0);
        BackingCatches.Should().ContainKey(catchId);
    }

    [Fact]
    public async Task ItShouldTreatTheExactBoundaryAsEligible()
    {
        // Arrange
        var cutoff = Now.AddHours(-24);
        await SaveSyncedCatchAsync(Guid.NewGuid(), OwnerUserId, syncedAt: cutoff);

        // Act
        var removed = await Sut.CleanupSyncedCacheAsync(OwnerUserId, cutoff, CancellationToken.None);

        // Assert
        removed.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldRetainAPendingCatchRegardlessOfAge()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        await SaveCatchAsync(
            catchId,
            OwnerUserId,
            SyncStatus.WaitingToSynchronise,
            SyncStatus.WaitingToSynchronise,
            photographStatus: SyncStatus.WaitingToSynchronise,
            syncedAt: Now.AddDays(-10));

        // Act
        var removed = await Sut.CleanupSyncedCacheAsync(OwnerUserId, Now.AddHours(-24), CancellationToken.None);

        // Assert
        removed.Should().Be(0);
        BackingCatches.Should().ContainKey(catchId);
    }

    [Fact]
    public async Task ItShouldRetainAFailedCatchRegardlessOfAge()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        await SaveCatchAsync(
            catchId,
            OwnerUserId,
            SyncStatus.FailedToSynchronise,
            SyncStatus.FailedToSynchronise,
            photographStatus: SyncStatus.FailedToSynchronise,
            syncedAt: Now.AddDays(-10));

        // Act
        var removed = await Sut.CleanupSyncedCacheAsync(OwnerUserId, Now.AddHours(-24), CancellationToken.None);

        // Assert
        removed.Should().Be(0);
        BackingCatches.Should().ContainKey(catchId);
    }

    [Fact]
    public async Task ItShouldRetainACatchWithAPhotographStillAwaitingUpload()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        await SaveCatchAsync(
            catchId,
            OwnerUserId,
            SyncStatus.Synchronised,
            SyncStatus.Synchronised,
            photographStatus: SyncStatus.WaitingToSynchronise,
            syncedAt: Now.AddDays(-2));

        // Act
        var removed = await Sut.CleanupSyncedCacheAsync(OwnerUserId, Now.AddHours(-24), CancellationToken.None);

        // Assert
        removed.Should().Be(0);
        BackingCatches.Should().ContainKey(catchId);
    }

    [Fact]
    public async Task ItShouldRetainACatchWhoseCurrentStateIsPendingEvenWithAnOldSyncedAtTimestamp()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        await SaveSyncedCatchAsync(catchId, OwnerUserId, syncedAt: Now.AddDays(-5));
        var resynced = BackingCatches[catchId] with
        {
            SyncStatus = SyncStatus.WaitingToSynchronise,
            MetadataSyncStatus = SyncStatus.WaitingToSynchronise
        };
        await Sut.UpdateSyncStateAsync(resynced, CancellationToken.None);

        // Act
        var removed = await Sut.CleanupSyncedCacheAsync(OwnerUserId, Now.AddHours(-24), CancellationToken.None);

        // Assert
        removed.Should().Be(0);
        BackingCatches.Should().ContainKey(catchId);
    }

    [Fact]
    public async Task ItShouldNotRemoveAnotherOwnersEligibleCatch()
    {
        // Arrange
        var ownerCatchId = Guid.NewGuid();
        var otherCatchId = Guid.NewGuid();
        await SaveSyncedCatchAsync(ownerCatchId, OwnerUserId, syncedAt: Now.AddHours(-25));
        await SaveSyncedCatchAsync(otherCatchId, OtherUserId, syncedAt: Now.AddHours(-25));

        // Act
        var removed = await Sut.CleanupSyncedCacheAsync(OwnerUserId, Now.AddHours(-24), CancellationToken.None);

        // Assert
        removed.Should().Be(1);
        BackingCatches.Should().NotContainKey(ownerCatchId);
        BackingCatches.Should().ContainKey(otherCatchId);
    }

    [Fact]
    public async Task ItShouldRemoveAFullySyncedCatchWithNoPhotographsOlderThanTheCutoff()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        SaveRawCatch(
            catchId,
            OwnerUserId,
            SyncStatus.Synchronised,
            SyncStatus.Synchronised,
            photographs: [],
            syncedAt: Now.AddHours(-25));

        // Act
        var removed = await Sut.CleanupSyncedCacheAsync(OwnerUserId, Now.AddHours(-24), CancellationToken.None);

        // Assert
        removed.Should().Be(1);
        BackingCatches.Should().NotContainKey(catchId);
    }

    [Fact]
    public async Task ItShouldRetainAFullySyncedCatchWithNoPhotographsNewerThanTheCutoff()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        SaveRawCatch(
            catchId,
            OwnerUserId,
            SyncStatus.Synchronised,
            SyncStatus.Synchronised,
            photographs: [],
            syncedAt: Now.AddHours(-23));

        // Act
        var removed = await Sut.CleanupSyncedCacheAsync(OwnerUserId, Now.AddHours(-24), CancellationToken.None);

        // Assert
        removed.Should().Be(0);
        BackingCatches.Should().ContainKey(catchId);
    }

    [Fact]
    public async Task ItShouldIgnoreCaughtOnWhenDecidingEligibility()
    {
        // Arrange
        var oldCatchDateRecentSync = Guid.NewGuid();
        await SaveCatchAsync(
            oldCatchDateRecentSync,
            OwnerUserId,
            SyncStatus.Synchronised,
            SyncStatus.Synchronised,
            photographStatus: SyncStatus.Synchronised,
            syncedAt: Now.AddHours(-1),
            caughtOn: Now.AddYears(-2));

        // Act
        var removed = await Sut.CleanupSyncedCacheAsync(OwnerUserId, Now.AddHours(-24), CancellationToken.None);

        // Assert
        removed.Should().Be(0);
        BackingCatches.Should().ContainKey(oldCatchDateRecentSync);
    }

    private void SaveRawCatch(
        Guid catchId,
        Guid ownerUserId,
        SyncStatus syncStatus,
        SyncStatus metadataSyncStatus,
        IReadOnlyList<CatchPhotographModel> photographs,
        DateTimeOffset syncedAt)
    {
        BackingCatches[catchId] = new CatchModel(
            catchId,
            Now,
            photographs,
            UserId: ownerUserId,
            SyncStatus: syncStatus,
            MetadataSyncStatus: metadataSyncStatus,
            SyncedAt: syncedAt);
    }

    private Task SaveSyncedCatchAsync(Guid catchId, Guid ownerUserId, DateTimeOffset syncedAt)
    {
        return SaveCatchAsync(
            catchId,
            ownerUserId,
            SyncStatus.Synchronised,
            SyncStatus.Synchronised,
            photographStatus: SyncStatus.Synchronised,
            syncedAt: syncedAt);
    }

    private async Task SaveCatchAsync(
        Guid catchId,
        Guid ownerUserId,
        SyncStatus syncStatus,
        SyncStatus metadataSyncStatus,
        SyncStatus photographStatus,
        DateTimeOffset syncedAt,
        DateTimeOffset? caughtOn = null)
    {
        var photographId = Guid.NewGuid();
        var catchRecord = new CatchModel(
            catchId,
            caughtOn ?? Now,
            [new CatchPhotographModel(photographId, catchId, PhotographContentTypeConstants.Jpeg, [1, 2, 3], photographStatus)],
            UserId: ownerUserId,
            SyncStatus: SyncStatus.SavedLocally,
            MetadataSyncStatus: SyncStatus.SavedLocally);
        await Sut.SaveAsync(catchRecord, CancellationToken.None);
        var withState = catchRecord with
        {
            SyncStatus = syncStatus,
            MetadataSyncStatus = metadataSyncStatus,
            SyncedAt = syncedAt
        };
        await Sut.UpdateSyncStateAsync(withState, CancellationToken.None);
    }
}
