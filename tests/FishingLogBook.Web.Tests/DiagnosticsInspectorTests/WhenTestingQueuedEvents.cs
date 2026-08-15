using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Diagnostics;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Pages.Diagnostics;
using NSubstitute;

namespace FishingLogBook.Web.Tests.DiagnosticsInspectorTests;

public class WhenTestingQueuedEvents : BaseDiagnosticsInspectorTest
{
    [Fact]
    public async Task ItShouldListQueuedEvents()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var queued = new DiagnosticEvent
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
        var queued = new DiagnosticEvent
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
                Task.FromResult<IReadOnlyList<DiagnosticEvent>>([]),
                Task.FromResult<IReadOnlyList<DiagnosticEvent>>([queued]));
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
}
