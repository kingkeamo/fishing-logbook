using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Pages.DiagnosticsInspector;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Diagnostics.Pages.DiagnosticsInspectorTests;

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
            .Returns(new DiagnosticProbeResultModel
            {
                DatabaseName = DiagnosticIndexedDbProbe.IsolatedDatabaseName,
                LastCompletedStage = DiagnosticIndexedDbProbe.StageCountReturned,
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
                .Contain(DiagnosticIndexedDbProbe.StageCountReturned);
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
        await store.DidNotReceive().EnqueueAsync(Arg.Any<DiagnosticEventModel>(), Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync(Arg.Any<DiagnosticEventModel>(), Arg.Any<CancellationToken>());
        await synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    private static IDiagnosticEventStore CreateUninitialisedStore()
    {
        var store = CreateStore();
        store.InspectExistingAsync(Arg.Any<CancellationToken>())
            .Returns(new DiagnosticDatabaseInspectionModel
            {
                Exists = false,
                HasStore = false,
                Count = 0
            });
        return store;
    }
}
