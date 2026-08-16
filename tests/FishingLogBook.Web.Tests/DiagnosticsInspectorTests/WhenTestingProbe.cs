using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Diagnostics;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Pages.Diagnostics;
using NSubstitute;

namespace FishingLogBook.Web.Tests.DiagnosticsInspectorTests;

public class WhenTestingProbe : BaseDiagnosticsInspectorTest
{
    [Fact]
    public async Task ItShouldShowTheIsolatedProbeStage()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var probe = Substitute.For<IDiagnosticIndexedDbProbe>();
        probe.RunIsolatedAsync(Arg.Any<CancellationToken>())
            .Returns(new DiagnosticProbeResult
            {
                DatabaseName = BrowserDiagnosticIndexedDbProbe.IsolatedDatabaseName,
                LastCompletedStage = BrowserDiagnosticIndexedDbProbe.StageDatabaseOpened,
                FailedStage = BrowserDiagnosticIndexedDbProbe.StageWriting,
                Error = "TimeoutException"
            });
        var store = CreateStore();
        store.InspectExistingAsync(Arg.Any<CancellationToken>())
            .Returns(new DiagnosticDatabaseInspection
            {
                Exists = true,
                HasStore = true,
                Count = 2
            });
        await using var context = CreateContext(store, probe: probe);

        // Act
        var cut = context.Render<DiagnosticsInspector>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#diagnostics-probe-isolated-stage").TextContent.Should()
                .Contain(BrowserDiagnosticIndexedDbProbe.StageDatabaseOpened);
            cut.Find("#diagnostics-probe-isolated-error").TextContent.Should()
                .Contain(BrowserDiagnosticIndexedDbProbe.StageWriting);
            cut.Find("#diagnostics-probe-production-stage").TextContent.Should()
                .Contain(BrowserDiagnosticIndexedDbProbe.StageCountReturned);
            cut.Find("#retry-diagnostics-probe-button").TextContent.Should()
                .Contain("Retry diagnostic probe");
        });
        await probe.Received(1).RunIsolatedAsync(Arg.Any<CancellationToken>());
        await store.Received().InspectExistingAsync(Arg.Any<CancellationToken>());
        await store.DidNotReceive().EnqueueAsync(Arg.Any<DiagnosticEvent>(), Arg.Any<CancellationToken>());
    }
}
