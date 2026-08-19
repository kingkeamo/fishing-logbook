using AwesomeAssertions;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Diagnostics.Clients;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage;
using FishingLogBook.Web.Features.Diagnostics.Storage.Stores;
using FishingLogBook.Web.Features.Diagnostics.Synchronisers;
using FishingLogBook.Web.Tests.Features.Diagnostics.TestSupport;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Diagnostics.Synchronisers.DiagnosticSynchroniserTests;

public class WhenTestingSynchronise
{
    [Fact]
    public async Task ItShouldUploadQueuedDiagnosticsInABatch_WhenOnline()
    {
        // Arrange
        var store = new MemoryDiagnosticEventStore();
        store.Items.Add(CreateEvent());
        store.Items.Add(CreateEvent());
        var client = Substitute.For<IDiagnosticClient>();
        var sut = CreateSut(store, client, online: true);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);

        // Assert
        await client.Received(1).UploadBatchAsync(
            Arg.Is<IReadOnlyList<FishingLogBook.Shared.Dtos.ClientDiagnosticEventDto>>(events => events.Count == 2),
            Arg.Any<CancellationToken>());
        store.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldClearEveryUploadedBatchFromTheQueue()
    {
        // Arrange
        var store = new MemoryDiagnosticEventStore();
        store.Items.Add(CreateEvent());
        store.Items.Add(CreateEvent());
        store.Items.Add(CreateEvent());
        var client = Substitute.For<IDiagnosticClient>();
        var sut = CreateSut(store, client, online: true, maxAttempts: 5, maxBatchSize: 2);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);

        // Assert
        store.Items.Should().BeEmpty();
        await client.Received(2).UploadBatchAsync(
            Arg.Any<IReadOnlyList<FishingLogBook.Shared.Dtos.ClientDiagnosticEventDto>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotUpload_WhenOffline()
    {
        // Arrange
        var store = new MemoryDiagnosticEventStore();
        store.Items.Add(CreateEvent());
        var client = Substitute.For<IDiagnosticClient>();
        var sut = CreateSut(store, client, online: false);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);

        // Assert
        await client.DidNotReceive().UploadBatchAsync(
            Arg.Any<IReadOnlyList<FishingLogBook.Shared.Dtos.ClientDiagnosticEventDto>>(),
            Arg.Any<CancellationToken>());
        store.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldRetryATransientFailureAndThenBoundTheRetry()
    {
        // Arrange
        var store = new MemoryDiagnosticEventStore();
        store.Items.Add(CreateEvent());
        var client = Substitute.For<IDiagnosticClient>();
        client.UploadBatchAsync(
                Arg.Any<IReadOnlyList<FishingLogBook.Shared.Dtos.ClientDiagnosticEventDto>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("transient"));
        var sut = CreateSut(store, client, online: true, maxAttempts: 2);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);
        await sut.SynchronisePendingAsync(CancellationToken.None);
        await sut.SynchronisePendingAsync(CancellationToken.None);

        // Assert
        store.Items.Should().BeEmpty();
        await client.Received(2).UploadBatchAsync(
            Arg.Any<IReadOnlyList<FishingLogBook.Shared.Dtos.ClientDiagnosticEventDto>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotBlockCatchSync_WhenDiagnosticUploadFails()
    {
        // Arrange
        var store = new MemoryDiagnosticEventStore();
        store.Items.Add(CreateEvent());
        var client = Substitute.For<IDiagnosticClient>();
        client.UploadBatchAsync(
                Arg.Any<IReadOnlyList<FishingLogBook.Shared.Dtos.ClientDiagnosticEventDto>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("diagnostics"));
        var sut = CreateSut(store, client, online: true);

        // Act
        var act = async () => await sut.SynchronisePendingAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        store.Items.Should().ContainSingle();
        store.Items[0].RetryCount.Should().Be(1);
        store.Items[0].SyncStatus.Should().Be(SyncStatus.FailedToSynchronise);
    }

    [Fact]
    public async Task ItShouldStopUploading_WhenTheQueueDoesNotShrink()
    {
        // Arrange
        var queued = CreateEvent();
        var store = Substitute.For<IDiagnosticEventStore>();
        store.GetCountAsync(Arg.Any<CancellationToken>()).Returns(1);
        store.GetPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DiagnosticEventModel>>([queued]));
        var client = Substitute.For<IDiagnosticClient>();
        var network = Substitute.For<INetworkService>();
        network.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(true);
        var sut = new DiagnosticSynchroniser(
            store,
            client,
            network,
            new DiagnosticStatusModel(),
            new DiagnosticsClientConfig { MaxBatchSize = 50, MaxUploadAttempts = 5, MaxQueueSize = 500 });

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);

        // Assert
        await client.Received(1).UploadBatchAsync(
            Arg.Is<IReadOnlyList<FishingLogBook.Shared.Dtos.ClientDiagnosticEventDto>>(events =>
                events.Count == 1 && events[0].Id == queued.Id),
            Arg.Any<CancellationToken>());
        await store.Received(1).RemoveAsync(
            Arg.Is<IReadOnlyList<Guid>>(ids => ids.Count == 1 && ids[0] == queued.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRecordTheQueueCountOperation_WhenCountIsCanceled()
    {
        // Arrange
        var store = Substitute.For<IDiagnosticEventStore>();
        store.GetCountAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("queue count canceled"));
        var status = new DiagnosticStatusModel();
        var sut = CreateSut(store, Substitute.For<IDiagnosticClient>(), online: true, status: status);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);

        // Assert
        status.QueueCountAvailable.Should().BeFalse();
        status.LastOperation.Should().Be(DiagnosticOperations.QueueCount);
        status.LastError.Should().Contain(DiagnosticOperations.QueueCount);
        status.LastError.Should().Contain(nameof(TaskCanceledException));
    }

    [Fact]
    public async Task ItShouldRecordTheUploadOperation_WhenHttpUploadIsCanceled()
    {
        // Arrange
        var store = new MemoryDiagnosticEventStore();
        store.Items.Add(CreateEvent());
        var client = Substitute.For<IDiagnosticClient>();
        client.UploadBatchAsync(
                Arg.Any<IReadOnlyList<FishingLogBook.Shared.Dtos.ClientDiagnosticEventDto>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("upload canceled"));
        var status = new DiagnosticStatusModel();
        var sut = CreateSut(store, client, online: true, status: status);

        // Act
        await sut.SynchronisePendingAsync(CancellationToken.None);

        // Assert
        status.LastOperation.Should().BeOneOf(DiagnosticOperations.Upload, DiagnosticOperations.FailedEventSave, DiagnosticOperations.QueueCount);
        status.LastError.Should().Contain(nameof(TaskCanceledException));
        status.LastError.Should().Contain(DiagnosticOperations.Upload);
        store.Items.Should().ContainSingle();
    }

    private static DiagnosticSynchroniser CreateSut(
        IDiagnosticEventStore store,
        IDiagnosticClient client,
        bool online,
        int maxAttempts = 5,
        int maxBatchSize = 50,
        DiagnosticStatusModel? status = null)
    {
        var network = Substitute.For<INetworkService>();
        network.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(online);
        return new DiagnosticSynchroniser(
            store,
            client,
            network,
            status ?? new DiagnosticStatusModel(),
            new DiagnosticsClientConfig { MaxBatchSize = maxBatchSize, MaxUploadAttempts = maxAttempts });
    }

    private static DiagnosticEventModel CreateEvent()
    {
        return new DiagnosticEventModel
        {
            Id = Guid.NewGuid(),
            TimestampUtc = DateTimeOffset.UtcNow,
            Level = FishingLogBook.Shared.Diagnostics.DiagnosticLevel.Warning,
            EventName = "OfflineDbWriteTimedOut",
            Message = "timed out"
        };
    }
}
