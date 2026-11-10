using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.Synchronisers.CatchSynchroniserTests;

public class WhenTestingOwnerGuards : BaseCatchSynchroniserTest
{
    [Fact]
    public async Task ItShouldRejectAnEmptyOwnerAndStillReportTheStateChange()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateCatch());
        var sut = CreateSut(store);
        var changes = 0;
        sut.StateChanged += (_, _) => changes++;

        // Act
        var act = () => sut.SynchronisePendingAsync(Guid.Empty, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        changes.Should().Be(1);
        await MockCatchClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDoNothingButReportTheStateChangeWhenNoOwnerIsSignedIn()
    {
        // Arrange
        MockLocalCatchOwner.GetUserIdAsync(Arg.Any<CancellationToken>())
            .Returns<Task<Guid>>(_ => throw new InvalidOperationException("No signed-in user."));
        var store = await CreateStoreAsync(CreateCatch());
        var sut = CreateSut(store);
        var changes = 0;
        sut.StateChanged += (_, _) => changes++;

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);

        // Assert
        changes.Should().Be(1);
        await MockLocalCatchOwner.Received(1).GetUserIdAsync(Arg.Any<CancellationToken>());
        await MockCatchClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        (await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None))!.SyncStatus.Should().Be(SyncStatus.SavedLocally);
    }

    [Fact]
    public async Task ItShouldNotCleanUpWhenNoOwnerIsSignedIn()
    {
        // Arrange
        MockLocalCatchOwner.GetUserIdAsync(Arg.Any<CancellationToken>())
            .Returns<Task<Guid>>(_ => throw new InvalidOperationException("No signed-in user."));
        var store = await CreateStoreAsync(CreateCatch());
        var sut = CreateSut(store);

        // Act
        await sut.CleanupSyncedCacheAsync(CancellationToken.None);

        // Assert
        await MockLocalCatchOwner.Received(1).GetUserIdAsync(Arg.Any<CancellationToken>());
        await MockNetworkService.DidNotReceive().IsOnlineAsync(Arg.Any<CancellationToken>());
        await MockCatchClient.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotCleanUpForAnEmptyOwner()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateCatch());
        var sut = CreateSut(store);

        // Act
        await sut.CleanupSyncedCacheAsync(Guid.Empty, CancellationToken.None);

        // Assert
        await MockNetworkService.DidNotReceive().IsOnlineAsync(Arg.Any<CancellationToken>());
        (await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldNotRetryWhenNoOwnerIsSignedIn()
    {
        // Arrange
        MockLocalCatchOwner.GetUserIdAsync(Arg.Any<CancellationToken>())
            .Returns<Task<Guid>>(_ => throw new InvalidOperationException("No signed-in user."));
        var store = await CreateStoreAsync(CreateCatch(catchStatus: SyncStatus.FailedToSynchronise));
        var sut = CreateSut(store);

        // Act
        await sut.RetryAsync(CatchId, CancellationToken.None);

        // Assert
        await MockLocalCatchOwner.Received(1).GetUserIdAsync(Arg.Any<CancellationToken>());
        await MockCatchClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        (await store.GetAsync(OwnerUserId, CatchId, CancellationToken.None))!.SyncStatus.Should().Be(SyncStatus.FailedToSynchronise);
    }

    [Fact]
    public async Task ItShouldIgnoreARetryForACatchThatDoesNotExist()
    {
        // Arrange
        var store = await CreateStoreAsync();
        var sut = CreateSut(store);

        // Act
        await sut.RetryAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        await MockCatchClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        await MockNetworkService.DidNotReceive().IsOnlineAsync(Arg.Any<CancellationToken>());
    }
}
