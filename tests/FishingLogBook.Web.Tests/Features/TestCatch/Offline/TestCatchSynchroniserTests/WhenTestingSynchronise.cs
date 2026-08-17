using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.SystemStatus.Services;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;
using FishingLogBook.Web.Features.TestCatch.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.TestCatch.Offline.TestCatchSynchroniserTests;

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
        var sut = new TestCatchSynchroniser(store, EmptyPhotos(), client, Online());

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
        var sut = new TestCatchSynchroniser(store, EmptyPhotos(), client, Online());

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
        var network = Substitute.For<INetworkService>();
        network.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        var sut = new TestCatchSynchroniser(store, EmptyPhotos(), client, network);

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
        var sut = new TestCatchSynchroniser(store, EmptyPhotos(), client, Online());

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
        var sut = new TestCatchSynchroniser(store, EmptyPhotos(), client, Online());

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

    [Fact]
    public async Task ItShouldKeepCatchSynchronised_WhenPhotographUploadFails()
    {
        // Arrange
        var photograph = new TestCatchPhotographModel(
            Guid.Parse("aa11bb22-cc33-dd44-ee55-ff6677889900"),
            "image/jpeg",
            SyncStatus.SavedLocally);
        var testCatch = CreateCatch(SyncStatus.SavedLocally) with { Photograph = photograph };
        var store = CreateStore([testCatch]);
        var photos = CreatePhotoStore(testCatch.Id, [1, 2, 3], "image/jpeg");
        var client = Substitute.For<ITestCatchClient>();
        client.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        client.CreatePhotographUploadAsync(Arg.Any<Guid>(), Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("storage unavailable"));
        var sut = new TestCatchSynchroniser(store, photos, client, Online());

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await store.GetAllAsync(CancellationToken.None);

        // Assert
        saved.Should().ContainSingle();
        saved[0].Id.Should().Be(testCatch.Id);
        saved[0].SyncStatus.Should().Be(SyncStatus.Synchronised);
        saved[0].Photograph!.SyncStatus.Should().Be(SyncStatus.FailedToSynchronise);
        await client.Received(1).UpsertAsync(
            Arg.Is<TestCatchDto>(dto => dto.Id == testCatch.Id),
            Arg.Any<CancellationToken>());
        await client.DidNotReceive().RecordPhotographAsync(
            Arg.Any<Guid>(),
            Arg.Any<RecordPhotographDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRetryTheSamePhotograph_WhenUploadIsRetried()
    {
        // Arrange
        var photograph = new TestCatchPhotographModel(
            Guid.Parse("aa11bb22-cc33-dd44-ee55-ff6677889900"),
            "image/jpeg",
            SyncStatus.FailedToSynchronise);
        var testCatch = CreateCatch(SyncStatus.Synchronised) with { Photograph = photograph };
        var store = CreateStore([testCatch]);
        var photos = CreatePhotoStore(testCatch.Id, [1, 2, 3], "image/jpeg");
        var client = Substitute.For<ITestCatchClient>();
        client.CreatePhotographUploadAsync(Arg.Any<Guid>(), Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("storage unavailable"));
        var sut = new TestCatchSynchroniser(store, photos, client, Online());

        // Act
        await sut.RetryPhotographAsync(testCatch.Id, CancellationToken.None);
        await sut.RetryPhotographAsync(testCatch.Id, CancellationToken.None);
        var saved = await store.GetAllAsync(CancellationToken.None);

        // Assert
        saved.Should().ContainSingle();
        saved[0].SyncStatus.Should().Be(SyncStatus.Synchronised);
        saved[0].Photograph!.Id.Should().Be(photograph.Id);
        saved[0].Photograph!.SyncStatus.Should().Be(SyncStatus.FailedToSynchronise);
        await client.DidNotReceive().UpsertAsync(Arg.Any<TestCatchDto>(), Arg.Any<CancellationToken>());
        await client.Received(2).CreatePhotographUploadAsync(
            testCatch.Id,
            Arg.Is<PhotographUploadRequestDto>(request => request.PhotographId == photograph.Id),
            Arg.Any<CancellationToken>());
        await client.DidNotReceive().RecordPhotographAsync(
            Arg.Any<Guid>(),
            Arg.Any<RecordPhotographDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowRemotePhotograph_WhenMergedFromApi()
    {
        // Arrange
        var store = CreateStore([]);
        var photographId = Guid.Parse("c0ffee00-1111-2222-3333-444455556666");
        var remote = new TestCatchDto(
            Guid.Parse("6f4c8a12-3e90-4b7d-a1c5-9d2e8f0b6a33"),
            "Bream",
            DateTimeOffset.Parse("2026-08-14T16:00:00Z"),
            null,
            photographId,
            "image/jpeg",
            "https://storage.test/download/photo");
        var client = Substitute.For<ITestCatchClient>();
        client.GetAllAsync(Arg.Any<CancellationToken>()).Returns([remote]);
        var sut = new TestCatchSynchroniser(store, EmptyPhotos(), client, Online());

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await store.GetAllAsync(CancellationToken.None);

        // Assert
        saved.Should().ContainSingle();
        saved[0].Photograph.Should().NotBeNull();
        saved[0].Photograph!.Id.Should().Be(photographId);
        saved[0].Photograph!.SyncStatus.Should().Be(SyncStatus.Synchronised);
        saved[0].Photograph!.RemoteUrl.Should().Be("https://storage.test/download/photo");
    }

    [Fact]
    public async Task ItShouldSendLocationWithTheCatch_WhenSynchronising()
    {
        // Arrange
        var location = new CatchLocationModel(
            53.2707,
            -9.0568,
            12,
            DateTimeOffset.Parse("2026-08-15T12:00:00Z"),
            "DeviceGps",
            "Private",
            "1");
        var testCatch = CreateCatch(SyncStatus.SavedLocally) with { Location = location };
        var store = CreateStore([testCatch]);
        var client = Substitute.For<ITestCatchClient>();
        client.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        var sut = new TestCatchSynchroniser(store, EmptyPhotos(), client, Online());

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);

        // Assert
        await client.Received(1).UpsertAsync(
            Arg.Is<TestCatchDto>(dto =>
                dto.Id == testCatch.Id &&
                dto.Location != null &&
                dto.Location.Latitude == location.Latitude &&
                dto.Location.Longitude == location.Longitude &&
                dto.Location.Visibility == "Private"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepRemoteLocation_WhenMergedFromApi()
    {
        // Arrange
        var store = CreateStore([]);
        var remoteLocation = new CatchLocationDto(
            53.2707,
            -9.0568,
            8,
            DateTimeOffset.Parse("2026-08-15T12:00:00Z"),
            "DeviceGps",
            "Private",
            "1");
        var remote = new TestCatchDto(
            Guid.Parse("6f4c8a12-3e90-4b7d-a1c5-9d2e8f0b6a33"),
            "Bream",
            DateTimeOffset.Parse("2026-08-14T16:00:00Z"),
            null,
            Location: remoteLocation);
        var client = Substitute.For<ITestCatchClient>();
        client.GetAllAsync(Arg.Any<CancellationToken>()).Returns([remote]);
        var sut = new TestCatchSynchroniser(store, EmptyPhotos(), client, Online());

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        var saved = await store.GetAllAsync(CancellationToken.None);

        // Assert
        saved.Should().ContainSingle();
        saved[0].Location.Should().NotBeNull();
        saved[0].Location!.Latitude.Should().Be(remoteLocation.Latitude);
        saved[0].Location!.Longitude.Should().Be(remoteLocation.Longitude);
        saved[0].Location!.Visibility.Should().Be("Private");
    }

    private static INetworkService Online()
    {
        var network = Substitute.For<INetworkService>();
        network.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(true);
        return network;
    }

    private static TestCatchModel CreateCatch(SyncStatus status)
    {
        return new TestCatchModel(
            Guid.Parse("2d8b6e40-1a57-4c3f-9e12-7b0c5d8a4f21"),
            "Pike",
            DateTimeOffset.Parse("2026-08-14T12:00:00Z"),
            "First attempt",
            status);
    }

    private static ITestCatchPhotoStore EmptyPhotos()
    {
        var photos = Substitute.For<ITestCatchPhotoStore>();
        photos.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TestCatchPhotoBytesModel?>(null));
        return photos;
    }

    private static ITestCatchPhotoStore CreatePhotoStore(Guid testCatchId, byte[] bytes, string contentType)
    {
        var photos = Substitute.For<ITestCatchPhotoStore>();
        photos.GetAsync(testCatchId, Arg.Any<CancellationToken>())
            .Returns(new TestCatchPhotoBytesModel(bytes, contentType));
        return photos;
    }

    private static ITestCatchStore CreateStore(IReadOnlyList<TestCatchModel> seed)
    {
        var items = seed.ToList();
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<TestCatchModel>>(items.ToArray()));
        store.SaveAsync(Arg.Any<TestCatchModel>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var saved = callInfo.Arg<TestCatchModel>();
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
