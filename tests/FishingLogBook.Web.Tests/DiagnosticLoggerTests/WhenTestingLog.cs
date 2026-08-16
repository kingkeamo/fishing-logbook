using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Diagnostics;
using FishingLogBook.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NSubstitute;

namespace FishingLogBook.Web.Tests.DiagnosticLoggerTests;

public class WhenTestingLog
{
    [Fact]
    public async Task ItShouldQueueAnOfflineDiagnosticEvent()
    {
        // Arrange
        var store = new MemoryDiagnosticEventStore();
        var sut = CreateLogger(store);

        // Act
        await sut.LogAsync(DiagnosticLevel.Warning, DiagnosticEventNames.OfflineDbWriteTimedOut, "timed out");

        // Assert
        store.Items.Should().ContainSingle();
        store.Items[0].EventName.Should().Be(DiagnosticEventNames.OfflineDbWriteTimedOut);
        store.Items[0].SyncStatus.Should().Be(FishingLogBook.Web.Models.SyncStatus.SavedLocally);
    }

    [Fact]
    public async Task ItShouldKeepQueuedEvents_WhenReloadedFromTheStore()
    {
        // Arrange
        var store = new MemoryDiagnosticEventStore();
        var first = CreateLogger(store);
        await first.LogAsync(DiagnosticLevel.Error, DiagnosticEventNames.CatchOfflineSaveFailed, "failed");

        // Act
        var pending = await store.GetPendingAsync(50, CancellationToken.None);

        // Assert
        pending.Should().ContainSingle()
            .Which.EventName.Should().Be(DiagnosticEventNames.CatchOfflineSaveFailed);
    }

    [Fact]
    public async Task ItShouldNotRecursivelyLog_WhenPersistenceFails()
    {
        // Arrange
        var store = new MemoryDiagnosticEventStore
        {
            ThrowOnEnqueue = new InvalidOperationException("indexeddb")
        };
        var sut = CreateLogger(store);

        // Act
        await sut.LogAsync(DiagnosticLevel.Error, DiagnosticEventNames.OfflineDbWriteFailed, "failed");

        // Assert
        store.EnqueueCalls.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldNotPersistDebugEventsInProductionConfiguration()
    {
        // Arrange
        var store = new MemoryDiagnosticEventStore();
        var sut = CreateLogger(store, new DiagnosticsClientConfig { MinimumPersistLevel = "Warning" });

        // Act
        await sut.LogAsync(DiagnosticLevel.Debug, DiagnosticEventNames.OfflineDbReadStarted, "started");

        // Assert
        store.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldFilterSensitiveMetadata()
    {
        // Arrange
        var store = new MemoryDiagnosticEventStore();
        var sut = CreateLogger(store);

        // Act
        await sut.LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.CatchOfflineSaveFailed,
            "failed",
            new Dictionary<string, string>
            {
                ["notes"] = "private",
                ["elapsedMilliseconds"] = "9"
            });

        // Assert
        store.Items[0].Metadata.Should().ContainKey("elapsedMilliseconds");
        store.Items[0].Metadata.Should().NotContainKey("notes");
    }

    [Fact]
    public async Task ItShouldReturnAndRecordTimeout_WhenPersistenceDoesNotComplete()
    {
        // Arrange
        var store = new MemoryDiagnosticEventStore
        {
            HangOnEnqueue = new TaskCompletionSource<bool>()
        };
        var status = new DiagnosticStatus();
        var sut = CreateLogger(
            store,
            new DiagnosticsClientConfig
            {
                MinimumPersistLevel = "Information",
                OperationTimeoutMilliseconds = 250
            },
            status);

        // Act
        await sut.LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.CatchOfflineSaveStarted,
            "started");

        // Assert
        store.Items.Should().BeEmpty();
        status.LastError.Should().Contain(DiagnosticOperations.Persist);
        status.LastError.Should().Contain(nameof(TimeoutException));
    }

    private static DiagnosticLogger CreateLogger(
        MemoryDiagnosticEventStore store,
        DiagnosticsClientConfig? config = null,
        DiagnosticStatus? status = null)
    {
        var network = Substitute.For<INetworkStatus>();
        network.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        return new DiagnosticLogger(
            store,
            status ?? new DiagnosticStatus(),
            config ?? new DiagnosticsClientConfig { MinimumPersistLevel = "Information" },
            new CorrelationContext(),
            network,
            new TestNavigation(),
            Substitute.For<IJSRuntime>(),
            Substitute.For<ILoggingService>());
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation()
        {
            Initialize("https://example.test/", "https://example.test/test-catch");
        }
    }
}
