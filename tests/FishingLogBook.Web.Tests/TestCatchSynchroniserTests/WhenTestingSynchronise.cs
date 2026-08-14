using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Models;
using FishingLogBook.Web.Offline;
using FishingLogBook.Web.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.TestCatchSynchroniserTests;

public class WhenTestingSynchronise
{
    [Fact]
    public async Task ItShouldMarkCatchSynchronised_WhenApiAcceptsIt()
    {
        // Arrange
        var testCatch = CreateCatch(SyncStatus.SavedLocally);
        var store = CreateStore([testCatch]);
        var client = Substitute.For<ITestCatchClient>();
        client.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        var sut = new TestCatchSynchroniser(store, client, Online());

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await store.GetAllAsync(CancellationToken.None);

        // Assert
        saved.Should().ContainSingle()
            .Which.SyncStatus.Should().Be(SyncStatus.Synchronised);
        await client.Received(1).UpsertAsync(
            Arg.Is<TestCatchDto>(dto => dto.Id == testCatch.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheLocalCatchAndMarkFailed_WhenApiCallFails()
    {
        // Arrange
        var testCatch = CreateCatch(SyncStatus.SavedLocally);
        var store = CreateStore([testCatch]);
        var client = Substitute.For<ITestCatchClient>();
        client.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        client.UpsertAsync(Arg.Any<TestCatchDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("network"));
        var sut = new TestCatchSynchroniser(store, client, Online());

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await store.GetAllAsync(CancellationToken.None);

        // Assert
        saved.Should().ContainSingle();
        saved[0].Id.Should().Be(testCatch.Id);
        saved[0].SyncStatus.Should().Be(SyncStatus.FailedToSynchronise);
    }

    [Fact]
    public async Task ItShouldLeaveCatchSavedLocally_WhenOffline()
    {
        // Arrange
        var testCatch = CreateCatch(SyncStatus.SavedLocally);
        var store = CreateStore([testCatch]);
        var client = Substitute.For<ITestCatchClient>();
        var network = Substitute.For<INetworkStatus>();
        network.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        var sut = new TestCatchSynchroniser(store, client, network);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await store.GetAllAsync(CancellationToken.None);

        // Assert
        saved.Should().ContainSingle()
            .Which.SyncStatus.Should().Be(SyncStatus.SavedLocally);
        await client.DidNotReceive().UpsertAsync(Arg.Any<TestCatchDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotCreateASecondServerRecord_WhenFailedCatchIsRetried()
    {
        // Arrange
        var testCatch = CreateCatch(SyncStatus.SavedLocally);
        var store = CreateStore([testCatch]);
        var client = Substitute.For<ITestCatchClient>();
        client.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        var sut = new TestCatchSynchroniser(store, client, Online());

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var afterFirst = (await store.GetAllAsync(CancellationToken.None))[0];
        await store.SaveAsync(afterFirst with { SyncStatus = SyncStatus.FailedToSynchronise }, CancellationToken.None);
        await sut.RetryAsync(testCatch.Id, CancellationToken.None);
        var saved = await store.GetAllAsync(CancellationToken.None);

        // Assert
        saved.Should().ContainSingle().Which.Id.Should().Be(testCatch.Id);
        await client.Received(2).UpsertAsync(
            Arg.Is<TestCatchDto>(dto => dto.Id == testCatch.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowServerCatchesAsSynchronised_WhenMergedFromApi()
    {
        // Arrange
        var store = CreateStore([]);
        var remote = new TestCatchDto(
            Guid.Parse("6f4c8a12-3e90-4b7d-a1c5-9d2e8f0b6a33"),
            "Bream",
            DateTimeOffset.Parse("2026-08-14T16:00:00Z"),
            null);
        var client = Substitute.For<ITestCatchClient>();
        client.GetAllAsync(Arg.Any<CancellationToken>()).Returns([remote]);
        var sut = new TestCatchSynchroniser(store, client, Online());

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await store.GetAllAsync(CancellationToken.None);

        // Assert
        saved.Should().ContainSingle();
        saved[0].Id.Should().Be(remote.Id);
        saved[0].SpeciesName.Should().Be("Bream");
        saved[0].SyncStatus.Should().Be(SyncStatus.Synchronised);
        await client.DidNotReceive().UpsertAsync(Arg.Any<TestCatchDto>(), Arg.Any<CancellationToken>());
    }

    private static INetworkStatus Online()
    {
        var network = Substitute.For<INetworkStatus>();
        network.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(true);
        return network;
    }

    private static TestCatch CreateCatch(SyncStatus status)
    {
        return new TestCatch(
            Guid.Parse("2d8b6e40-1a57-4c3f-9e12-7b0c5d8a4f21"),
            "Pike",
            DateTimeOffset.Parse("2026-08-14T12:00:00Z"),
            "First attempt",
            status);
    }

    private static ITestCatchStore CreateStore(IReadOnlyList<TestCatch> seed)
    {
        var items = seed.ToList();
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<TestCatch>>(items.ToArray()));
        store.SaveAsync(Arg.Any<TestCatch>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var saved = callInfo.Arg<TestCatch>();
                var index = items.FindIndex(item => item.Id == saved.Id);
                if (index >= 0)
                {
                    items[index] = saved;
                }
                else
                {
                    items.Add(saved);
                }

                return Task.CompletedTask;
            });
        return store;
    }
}
