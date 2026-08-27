using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Synchronisers.TripPhotographSynchroniserTests;

public class WhenTestingSynchronisePending : BaseTripPhotographSynchroniserTest
{
    [Fact]
    public async Task ItShouldDoNothingWhenTheOwnerIsUnknown()
    {
        // Arrange
        var store = await CreateStoreAsync(CreatePhotograph());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(Guid.Empty, CancellationToken.None);

        // Assert
        store.PendingCalls.Should().Be(0);
        await MockTripClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotContactTheServerWhenNothingIsPending()
    {
        // Arrange
        var store = await CreateStoreAsync(CreatePhotograph(syncStatus: SyncStatus.Synchronised));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockNetworkService.DidNotReceive().IsOnlineAsync(Arg.Any<CancellationToken>());
        await MockTripClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLeaveWorkPendingWhileOffline()
    {
        // Arrange
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        var store = await CreateStoreAsync(CreatePhotograph());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
        store.Stored(PhotographId)!.SyncStatus.Should().Be(SyncStatus.SavedLocally);
    }

    [Fact]
    public async Task ItShouldWaitWhenTheParentTripHasNotReachedTheServer()
    {
        // Arrange
        MockTripDependency.IsTripReadyForServerAsync(
                OwnerUserId,
                TripId,
                Arg.Any<CancellationToken>())
            .Returns(false);
        var store = await CreateStoreAsync(CreatePhotograph());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
        store.Stored(PhotographId)!.SyncStatus.Should().Be(SyncStatus.SavedLocally);
        store.BytesReadFor.Should().BeEmpty();
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Information,
            DiagnosticEventNames.TripPhotoSyncWaitingForTrip,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillUploadPhotographsForAReadyTrip()
    {
        // Arrange
        MockTripDependency.IsTripReadyForServerAsync(
                OwnerUserId,
                TripId,
                Arg.Any<CancellationToken>())
            .Returns(false);
        var store = await CreateStoreAsync(
            CreatePhotograph(),
            CreatePhotograph(SecondPhotographId, OtherTripId));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripClient.Received(1).CreatePhotographUploadAsync(
            OtherTripId,
            Arg.Is<PhotographUploadRequestDto>(request => request.PhotographId == SecondPhotographId),
            Arg.Any<CancellationToken>());
        store.Stored(SecondPhotographId)!.SyncStatus.Should().Be(SyncStatus.Synchronised);
        store.Stored(PhotographId)!.SyncStatus.Should().Be(SyncStatus.SavedLocally);
    }

    [Fact]
    public async Task ItShouldKeepThePhotographPendingWhenTheUploadFails()
    {
        // Arrange
        MockTripClient.UploadPhotographAsync(
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => throw new HttpRequestException("Storage unavailable."));
        var store = await CreateStoreAsync(CreatePhotograph());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        store.Stored(PhotographId)!.SyncStatus.Should().Be(SyncStatus.SavedLocally);
        store.Stored(PhotographId)!.SyncedAt.Should().BeNull();
        await MockTripClient.DidNotReceive().RecordPhotographAsync(
            Arg.Any<Guid>(),
            Arg.Any<RecordTripPhotographDto>(),
            Arg.Any<CancellationToken>());
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Error,
            DiagnosticEventNames.TripPhotoSyncFailed,
            Arg.Any<string>(),
            Arg.Is<IReadOnlyDictionary<string, string>>(metadata =>
                metadata[DiagnosticMetadata.ErrorType] == nameof(HttpRequestException)),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRetryAFailedUploadOnTheNextRunAndRecordItOnce()
    {
        // Arrange
        var attempts = 0;
        MockTripClient.UploadPhotographAsync(
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                attempts++;
                return attempts == 1
                    ? throw new HttpRequestException("Storage unavailable.")
                    : Task.CompletedTask;
            });
        var store = await CreateStoreAsync(CreatePhotograph());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        attempts.Should().Be(2);
        store.Stored(PhotographId)!.SyncStatus.Should().Be(SyncStatus.Synchronised);
        await MockTripClient.Received(1).RecordPhotographAsync(
            TripId,
            Arg.Is<RecordTripPhotographDto>(request => request.PhotographId == PhotographId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotResurrectAPhotographRemovedWhileItWasUploading()
    {
        // Arrange
        var store = await CreateStoreAsync(CreatePhotograph());
        var sut = CreateSut(store);
        store.BeforeByteRead = async photographId =>
        {
            store.BeforeByteRead = null;
            await store.DeleteAsync(OwnerUserId, TripId, photographId, CancellationToken.None);
        };

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        store.Count.Should().Be(0);
        store.Stored(PhotographId).Should().BeNull();
    }

    [Fact]
    public async Task ItShouldNotSendTheSamePhotographTwiceConcurrently()
    {
        // Arrange
        var store = await CreateStoreAsync(CreatePhotograph());
        var sut = CreateSut(store);
        var release = new TaskCompletionSource();
        MockTripClient.UploadPhotographAsync(
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => release.Task);

        // Act
        var first = sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);
        var second = sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);
        release.SetResult();
        await Task.WhenAll(first, second);

        // Assert
        await MockTripClient.Received(1).RecordPhotographAsync(
            TripId,
            Arg.Is<RecordTripPhotographDto>(request => request.PhotographId == PhotographId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldUploadTheBytesBeforeRecordingThePhotograph()
    {
        // Arrange
        var order = new List<string>();
        MockTripClient.UploadPhotographAsync(
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                order.Add("upload");
                return Task.CompletedTask;
            });
        MockTripClient.RecordPhotographAsync(
                Arg.Any<Guid>(),
                Arg.Any<RecordTripPhotographDto>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                order.Add("record");
                return Task.FromResult<TripPhotographDto?>(null);
            });
        var store = await CreateStoreAsync(CreatePhotograph());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        order.Should().Equal("upload", "record");
    }

    [Fact]
    public async Task ItShouldSendConsistentIdsKeysAndContentTypeThroughTheUploadSequence()
    {
        // Arrange
        var capturedOn = AddedOn.AddHours(-1);
        var expectedKey = $"trips/{OwnerUserId:D}/{TripId:D}/{PhotographId:D}";
        var store = await CreateStoreAsync(CreatePhotograph(capturedOn: capturedOn));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripClient.Received(1).CreatePhotographUploadAsync(
            TripId,
            Arg.Is<PhotographUploadRequestDto>(request =>
                request.PhotographId == PhotographId && request.ContentType == "image/jpeg"),
            Arg.Any<CancellationToken>());
        await MockTripClient.Received(1).UploadPhotographAsync(
            $"https://storage.test/{PhotographId:D}",
            Arg.Is<byte[]>(bytes => bytes.Length == 3),
            "image/jpeg",
            Arg.Any<CancellationToken>());
        await MockTripClient.Received(1).RecordPhotographAsync(
            TripId,
            Arg.Is<RecordTripPhotographDto>(request =>
                request.PhotographId == PhotographId
                && request.ObjectKey == expectedKey
                && request.ContentType == "image/jpeg"
                && request.AddedOn == AddedOn
                && request.CapturedOn == capturedOn),
            Arg.Any<CancellationToken>());
        var stored = store.Stored(PhotographId)!;
        stored.SyncStatus.Should().Be(SyncStatus.Synchronised);
        stored.ObjectKey.Should().Be(expectedKey);
        stored.SyncedAt.Should().NotBeNull();
    }
}
