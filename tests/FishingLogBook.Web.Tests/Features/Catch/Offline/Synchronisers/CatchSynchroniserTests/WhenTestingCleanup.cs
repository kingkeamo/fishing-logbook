using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Offline.Synchronisers;
using FishingLogBook.Web.Tests.Features.Catch.Offline.Stores.CatchStoreTests;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.Synchronisers.CatchSynchroniserTests;

public class WhenTestingCleanup : BaseCatchSynchroniserTest
{
    private CatchSynchroniser CreateSut(ICatchStore store)
    {
        return new CatchSynchroniser(
            store,
            MockTripDependency,
            MockCatchClient,
            MockNetworkService,
            MockLocalCatchOwner,
            MockDiagnostics,
            MockLogging);
    }

    [Fact]
    public async Task ItShouldStampSyncedAtWhenACatchFullySynchronises()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;
        var store = await CreateStoreAsync(CreateCatch());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);

        // Assert
        saved!.SyncStatus.Should().Be(SyncStatus.Synchronised);
        saved.SyncedAt.Should().NotBeNull();
        saved.SyncedAt.Should().BeOnOrAfter(before);
        saved.SyncedAt.Should().BeOnOrBefore(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ItShouldCleanUpTheResolvedOwnersSyncedCacheWhenOnline()
    {
        // Arrange
        var store = new MemoryCatchStore();
        var sut = CreateSut(store);

        // Act
        await sut.CleanupSyncedCacheAsync(CancellationToken.None);

        // Assert
        await MockLocalCatchOwner.Received(1).GetUserIdAsync(Arg.Any<CancellationToken>());
        store.CleanupCalls.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldCleanUpAnExplicitOwnersSyncedCacheWhenOnline()
    {
        // Arrange
        var store = new MemoryCatchStore();
        var sut = CreateSut(store);

        // Act
        await sut.CleanupSyncedCacheAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockLocalCatchOwner.DidNotReceive().GetUserIdAsync(Arg.Any<CancellationToken>());
        store.CleanupCalls.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldUseAnApproximatelyTwentyFourHourRetentionWindow()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        var sut = CreateSut(store);
        var before = DateTimeOffset.UtcNow;

        // Act
        await sut.CleanupSyncedCacheAsync(OwnerUserId, CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        // Assert
        await store.Received(1).CleanupSyncedCacheAsync(
            OwnerUserId,
            Arg.Is<DateTimeOffset>(cutoff =>
                cutoff >= before.AddHours(-24).AddSeconds(-5)
                && cutoff <= after.AddHours(-24).AddSeconds(5)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotCleanUpACatchWithANewerPendingEditAfterAnEarlierSuccessfulSync()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateCatch());
        var sut = CreateSut(store);
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var synced = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);
        synced!.SyncStatus.Should().Be(SyncStatus.Synchronised);
        var backdated = synced with { SyncedAt = DateTimeOffset.UtcNow.AddHours(-25) };
        await store.UpdateSyncStateAsync(backdated, CancellationToken.None);
        var editedAfterSync = backdated with
        {
            SpeciesName = "Perch",
            SyncStatus = SyncStatus.WaitingToSynchronise,
            MetadataSyncStatus = SyncStatus.WaitingToSynchronise
        };
        await store.UpdateSyncStateAsync(editedAfterSync, CancellationToken.None);

        // Act
        await sut.CleanupSyncedCacheAsync(OwnerUserId, CancellationToken.None);

        // Assert
        var stillPresent = await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None);
        stillPresent.Should().NotBeNull();
        stillPresent!.SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
    }

    [Fact]
    public async Task ItShouldNotCleanUpWhileOffline()
    {
        // Arrange
        var store = new MemoryCatchStore();
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        var sut = CreateSut(store);

        // Act
        await sut.CleanupSyncedCacheAsync(OwnerUserId, CancellationToken.None);

        // Assert
        store.CleanupCalls.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldDoNothingForAnEmptyOwner()
    {
        // Arrange
        var store = new MemoryCatchStore();
        var sut = CreateSut(store);

        // Act
        await sut.CleanupSyncedCacheAsync(Guid.Empty, CancellationToken.None);

        // Assert
        store.CleanupCalls.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldNotCleanUpWhenTheOwnerCannotBeResolved()
    {
        // Arrange
        var store = new MemoryCatchStore();
        MockLocalCatchOwner.GetUserIdAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("no local owner"));
        var sut = CreateSut(store);

        // Act
        var act = () => sut.CleanupSyncedCacheAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        store.CleanupCalls.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldNotThrowWhenTheStoreFailsToCleanUp()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        store.CleanupSyncedCacheAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("cleanup failed"));
        var sut = CreateSut(store);

        // Act
        var act = () => sut.CleanupSyncedCacheAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.CatchCacheCleanupFailed,
            Arg.Any<string>(),
            Arg.Is<IReadOnlyDictionary<string, string>>(metadata =>
                metadata[DiagnosticMetadata.ErrorType] == nameof(InvalidOperationException)),
            null,
            CancellationToken.None);
    }
}
