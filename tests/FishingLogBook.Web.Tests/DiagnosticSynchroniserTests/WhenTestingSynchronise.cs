using AwesomeAssertions;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Diagnostics;
using FishingLogBook.Web.Models;
using FishingLogBook.Web.Services;
using FishingLogBook.Web.Tests.DiagnosticLoggerTests;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.DiagnosticSynchroniserTests;

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

    private static DiagnosticSynchroniser CreateSut(
        MemoryDiagnosticEventStore store,
        IDiagnosticClient client,
        bool online,
        int maxAttempts = 5)
    {
        var network = Substitute.For<INetworkStatus>();
        network.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(online);
        return new DiagnosticSynchroniser(
            store,
            client,
            network,
            new DiagnosticStatus(),
            new DiagnosticsClientConfig { MaxBatchSize = 50, MaxUploadAttempts = maxAttempts });
    }

    private static DiagnosticEvent CreateEvent()
    {
        return new DiagnosticEvent
        {
            Id = Guid.NewGuid(),
            TimestampUtc = DateTimeOffset.UtcNow,
            Level = FishingLogBook.Shared.Diagnostics.DiagnosticLevel.Warning,
            EventName = "OfflineDbWriteTimedOut",
            Message = "timed out"
        };
    }
}
