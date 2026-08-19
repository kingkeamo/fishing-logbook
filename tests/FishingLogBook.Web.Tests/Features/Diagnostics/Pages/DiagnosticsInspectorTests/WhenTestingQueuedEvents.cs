using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Pages.DiagnosticsInspector;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage;
using FishingLogBook.Web.Features.Diagnostics.Synchronisers;
using FishingLogBook.Web.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Diagnostics.Pages.DiagnosticsInspectorTests;

public class WhenTestingQueuedEvents : BaseDiagnosticsInspectorTest
{
    [Fact]
    public async Task ItShouldListQueuedEvents()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var queued = new DiagnosticEventModel
        {
            Id = Guid.Parse("3a1f2c8e-7b44-4d1a-9c3e-2f8a6b0d1e55"),
            TimestampUtc = DateTimeOffset.Parse("2026-08-15T08:00:00Z"),
            Level = DiagnosticLevel.Warning,
            EventName = DiagnosticEventNames.OfflineDbReadTimedOut,
            Message = "read timed out."
        };
        var store = CreateStore(queued);
        var synchroniser = Substitute.For<IDiagnosticSynchroniser>();
        await using var context = CreateContext(store, synchroniser);

        // Act
        var cut = context.Render<DiagnosticsInspector>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#diagnostics-queued-count").TextContent.Should().Contain("Queued events");
            cut.Find("#diagnostics-queued-count").TextContent.Should().Contain("1");
            cut.Find($"#diagnostic-event-{queued.Id}").Should().NotBeNull();
            cut.Find($"#diagnostic-event-name-{queued.Id}").TextContent.Should()
                .Contain(DiagnosticEventNames.OfflineDbReadTimedOut);
            cut.Find($"#diagnostic-event-message-{queued.Id}").TextContent.Should()
                .Contain("read timed out.");
        });
        await synchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await store.Received(1).GetPendingAsync(500, Arg.Any<CancellationToken>());
        await store.Received(1).GetCountAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowEmptyQueue()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = CreateStore();
        var synchroniser = Substitute.For<IDiagnosticSynchroniser>();
        await using var context = CreateContext(store, synchroniser);

        // Act
        var cut = context.Render<DiagnosticsInspector>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#diagnostic-queue-empty").TextContent.Should()
                .Contain("No diagnostic events are queued");
            cut.FindAll("#diagnostic-queue").Should().BeEmpty();
        });
        await synchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await store.Received(1).GetPendingAsync(500, Arg.Any<CancellationToken>());
        await store.Received(1).GetCountAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotTreatAFailedCountAsAnEmptyQueue()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = CreateStore();
        store.GetCountAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("queue count timed out"));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<DiagnosticsInspector>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#diagnostics-queued-count").TextContent.Should().Contain("Unable to read queue");
            cut.Find("#diagnostics-queued-count").TextContent.Should().NotContain("0");
            cut.Find("#diagnostics-last-error").TextContent.Should().Contain(DiagnosticOperations.QueueCount);
            cut.Find("#diagnostics-last-error").TextContent.Should().Contain(nameof(TimeoutException));
            cut.Find("#diagnostics-last-operation").TextContent.Should().Contain(DiagnosticOperations.QueueCount);
            cut.FindAll("#diagnostic-queue-empty").Should().BeEmpty();
        });
    }

    [Fact]
    public async Task ItShouldShowFrenchEmptyQueue()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var store = CreateStore();
        var synchroniser = Substitute.For<IDiagnosticSynchroniser>();
        await using var context = CreateContext(store, synchroniser);

        // Act
        var cut = context.Render<DiagnosticsInspector>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#diagnostic-queue-empty").TextContent.Should()
                .Contain("Aucun événement de diagnostic n’est en file");
            cut.Find("#refresh-diagnostics-button").TextContent.Should().Contain("Actualiser");
        });
        await synchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await store.Received(1).GetPendingAsync(500, Arg.Any<CancellationToken>());
        await store.Received(1).GetCountAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReloadQueuedEvents_WhenRefreshIsClicked()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var queued = new DiagnosticEventModel
        {
            Id = Guid.Parse("8c0e91a2-4d77-4b18-a6f1-0c3d5e7a9b21"),
            TimestampUtc = DateTimeOffset.Parse("2026-08-15T08:10:00Z"),
            Level = DiagnosticLevel.Information,
            EventName = DiagnosticEventNames.CatchOfflineSaveCompleted,
            Message = "Catch offline save completed."
        };
        var store = CreateStore();
        var synchroniser = Substitute.For<IDiagnosticSynchroniser>();
        store.GetCountAsync(Arg.Any<CancellationToken>()).Returns(0, 1);
        store.GetPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IReadOnlyList<DiagnosticEventModel>>([]),
                Task.FromResult<IReadOnlyList<DiagnosticEventModel>>([queued]));
        await using var context = CreateContext(store, synchroniser);
        var cut = context.Render<DiagnosticsInspector>();
        cut.WaitForAssertion(() => cut.Find("#refresh-diagnostics-button").Should().NotBeNull());

        // Act
        await cut.Find("#refresh-diagnostics-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#diagnostic-event-{queued.Id}").Should().NotBeNull();
            cut.Find($"#diagnostic-event-name-{queued.Id}").TextContent.Should()
                .Contain(DiagnosticEventNames.CatchOfflineSaveCompleted);
        });
        await synchroniser.Received(2).SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await store.Received(2).GetPendingAsync(500, Arg.Any<CancellationToken>());
        await store.Received(2).GetCountAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheErrorType_WhenQueueReadFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = CreateStore();
        store.GetPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("queue read timed out"));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<DiagnosticsInspector>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#diagnostics-last-error").TextContent.Should().Contain(nameof(TimeoutException));
        });
    }

    [Fact]
    public async Task ItShouldShowLastErrorFromLoggingService()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = CreateStore();
        var logging = SilentLogging();
        logging.GetLastErrorAsync(Arg.Any<CancellationToken>()).Returns(new LastErrorLog
        {
            TimestampUtc = DateTimeOffset.Parse("2026-08-15T08:00:00Z"),
            Source = "diagnostics refresh",
            ErrorType = nameof(TimeoutException),
            Message = "queue read timed out"
        });
        await using var context = CreateContext(store, logging: logging);

        // Act
        var cut = context.Render<DiagnosticsInspector>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#diagnostics-last-error").TextContent.Should().Contain(nameof(TimeoutException));
            cut.Find("#diagnostics-last-error").TextContent.Should().Contain("queue read timed out");
        });
    }
}
