using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Models;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Synchronisers.TripSynchroniserTests;

public class WhenTestingCleanupSyncedCache : BaseTripSynchroniserTest
{
    private static readonly DateTimeOffset LongAgo =
        DateTimeOffset.UtcNow.AddDays(-30);
    private static readonly DateTimeOffset JustNow =
        DateTimeOffset.UtcNow.AddMinutes(-5);

    [Fact]
    public async Task ItShouldDoNothingWhenTheOwnerIsUnknown()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateSyncedTrip(LongAgo));
        var sut = CreateSut(store);

        // Act
        await sut.CleanupSyncedCacheAsync(Guid.Empty, CancellationToken.None);

        // Assert
        store.CleanupCalls.Should().Be(0);
        var all = await store.GetAllAsync(OwnerUserId, CancellationToken.None);
        all.Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldNotCleanUpWhileOffline()
    {
        // Arrange
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        var store = await CreateStoreAsync(CreateSyncedTrip(LongAgo));
        var sut = CreateSut(store);

        // Act
        await sut.CleanupSyncedCacheAsync(OwnerUserId, CancellationToken.None);

        // Assert
        store.CleanupCalls.Should().Be(0);
        var all = await store.GetAllAsync(OwnerUserId, CancellationToken.None);
        all.Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldNotFailTheRunWhenTheStoreThrows()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateSyncedTrip(LongAgo));
        store.FailCleanup = true;
        var sut = CreateSut(store);

        // Act
        var act = async () =>
            await sut.CleanupSyncedCacheAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.TripCacheCleanupFailed,
            Arg.Any<string>(),
            Arg.Is<IReadOnlyDictionary<string, string>>(metadata =>
                metadata[DiagnosticMetadata.ErrorType] == nameof(InvalidOperationException)),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
        MockActiveTripService.DidNotReceive().Invalidate();
    }

    [Fact]
    public async Task ItShouldKeepATripThatHasNotSynchronised()
    {
        // Arrange
        var store = await CreateStoreAsync(
            CreateTrip(
                status: TripConstants.Completed,
                endedOn: StartedOn.AddHours(2),
                syncStatus: SyncStatus.SavedLocally,
                syncedAt: LongAgo));
        var sut = CreateSut(store);

        // Act
        await sut.CleanupSyncedCacheAsync(OwnerUserId, CancellationToken.None);

        // Assert
        var all = await store.GetAllAsync(OwnerUserId, CancellationToken.None);
        all.Should().ContainSingle();
        MockActiveTripService.DidNotReceive().Invalidate();
    }

    [Fact]
    public async Task ItShouldKeepAnActiveTripHoweverOldItIs()
    {
        // Arrange
        var store = await CreateStoreAsync(
            CreateTrip(syncStatus: SyncStatus.Synchronised, syncedAt: LongAgo));
        var sut = CreateSut(store);

        // Act
        await sut.CleanupSyncedCacheAsync(OwnerUserId, CancellationToken.None);

        // Assert
        var stored = await store.GetActiveAsync(OwnerUserId, CancellationToken.None);
        stored.Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldKeepARecentlySynchronisedTrip()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateSyncedTrip(JustNow));
        var sut = CreateSut(store);

        // Act
        await sut.CleanupSyncedCacheAsync(OwnerUserId, CancellationToken.None);

        // Assert
        var all = await store.GetAllAsync(OwnerUserId, CancellationToken.None);
        all.Should().ContainSingle();
        MockActiveTripService.DidNotReceive().Invalidate();
    }

    [Fact]
    public async Task ItShouldNotCleanUpAnotherAnglersTrip()
    {
        // Arrange
        var store = await CreateStoreAsync(
            CreateSyncedTrip(LongAgo, ownerUserId: OtherUserId));
        var sut = CreateSut(store);

        // Act
        await sut.CleanupSyncedCacheAsync(OwnerUserId, CancellationToken.None);

        // Assert
        var theirs = await store.GetAllAsync(OtherUserId, CancellationToken.None);
        theirs.Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldRemoveASyncedTripOnceTheRetentionWindowHasPassed()
    {
        // Arrange
        var store = await CreateStoreAsync(
            CreateSyncedTrip(LongAgo),
            CreateSyncedTrip(JustNow, tripId: SecondTripId));
        var sut = CreateSut(store);

        // Act
        await sut.CleanupSyncedCacheAsync(OwnerUserId, CancellationToken.None);

        // Assert
        var all = await store.GetAllAsync(OwnerUserId, CancellationToken.None);
        all.Should().ContainSingle(trip => trip.Id == SecondTripId);
        MockActiveTripService.Received(1).Invalidate();
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Debug,
            DiagnosticEventNames.TripCacheCleanupCompleted,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    private static TripModel CreateSyncedTrip(
        DateTimeOffset syncedAt,
        Guid? tripId = null,
        Guid? ownerUserId = null)
    {
        return CreateTrip(
            tripId: tripId,
            ownerUserId: ownerUserId,
            status: TripConstants.Completed,
            endedOn: StartedOn.AddHours(2),
            syncStatus: SyncStatus.Synchronised,
            syncedAt: syncedAt);
    }
}
