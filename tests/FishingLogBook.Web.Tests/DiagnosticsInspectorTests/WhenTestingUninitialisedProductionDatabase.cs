using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Diagnostics;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Pages.Diagnostics;
using NSubstitute;

namespace FishingLogBook.Web.Tests.DiagnosticsInspectorTests;

public class WhenTestingUninitialisedProductionDatabase : BaseDiagnosticsInspectorTest
{
    [Fact]
    public async Task ItShouldShowProductionDatabaseNotInitialised()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = CreateUninitialisedStore();
        var probe = Substitute.For<IDiagnosticIndexedDbProbe>();
        probe.RunIsolatedAsync(Arg.Any<CancellationToken>())
            .Returns(new DiagnosticProbeResult
            {
                DatabaseName = BrowserDiagnosticIndexedDbProbe.IsolatedDatabaseName,
                LastCompletedStage = BrowserDiagnosticIndexedDbProbe.StageCountReturned,
                Count = 1
            });
        await using var context = CreateContext(store, probe: probe);

        // Act
        var cut = context.Render<DiagnosticsInspector>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#diagnostics-probe-production-stage").TextContent.Should()
                .Contain("Production diagnostic database not initialised");
            cut.Find("#diagnostics-probe-isolated-stage").TextContent.Should()
                .Contain(BrowserDiagnosticIndexedDbProbe.StageCountReturned);
            cut.FindAll("#diagnostics-probe-production-error").Should().BeEmpty();
        });
        await probe.Received(1).RunIsolatedAsync(Arg.Any<CancellationToken>());
        await store.Received().InspectExistingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotReadTheProductionQueue()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = CreateUninitialisedStore();
        var synchroniser = Substitute.For<IDiagnosticSynchroniser>();
        await using var context = CreateContext(store, synchroniser);

        // Act
        var cut = context.Render<DiagnosticsInspector>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#diagnostics-probe-production-stage").TextContent.Should()
                .Contain("Production diagnostic database not initialised");
            cut.Find("#diagnostics-queued-count").TextContent.Should().Contain("Unable to read queue");
            cut.Find("#diagnostics-queued-count").TextContent.Should().NotContain("0");
            cut.FindAll("#diagnostic-queue-empty").Should().BeEmpty();
        });
        await store.Received().InspectExistingAsync(Arg.Any<CancellationToken>());
        await store.DidNotReceive().GetCountAsync(Arg.Any<CancellationToken>());
        await store.DidNotReceive().GetPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await store.DidNotReceive().EnqueueAsync(Arg.Any<DiagnosticEvent>(), Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync(Arg.Any<DiagnosticEvent>(), Arg.Any<CancellationToken>());
        await synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    private static IDiagnosticEventStore CreateUninitialisedStore()
    {
        var store = CreateStore();
        store.InspectExistingAsync(Arg.Any<CancellationToken>())
            .Returns(new DiagnosticDatabaseInspection
            {
                Exists = false,
                HasStore = false,
                Count = 0
            });
        return store;
    }
}
