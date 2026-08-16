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
        probe.RunAsync(
                BrowserDiagnosticIndexedDbProbe.IsolatedDatabaseName,
                true,
                Arg.Any<CancellationToken>())
            .Returns(new DiagnosticProbeResult
            {
                DatabaseName = BrowserDiagnosticIndexedDbProbe.IsolatedDatabaseName,
                LastCompletedStage = BrowserDiagnosticIndexedDbProbe.StageDatabaseOpened,
                FailedStage = BrowserDiagnosticIndexedDbProbe.StageWriting,
                Error = "TimeoutException"
            });
        probe.RunAsync(
                BrowserDiagnosticIndexedDbProbe.ProductionDatabaseName,
                false,
                Arg.Any<CancellationToken>())
            .Returns(new DiagnosticProbeResult
            {
                DatabaseName = BrowserDiagnosticIndexedDbProbe.ProductionDatabaseName,
                LastCompletedStage = BrowserDiagnosticIndexedDbProbe.StageCountReturned,
                Count = 2
            });
        await using var context = CreateContext(CreateStore(), probe: probe);

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
        await probe.Received(1).RunAsync(
            BrowserDiagnosticIndexedDbProbe.IsolatedDatabaseName,
            true,
            Arg.Any<CancellationToken>());
        await probe.Received(1).RunAsync(
            BrowserDiagnosticIndexedDbProbe.ProductionDatabaseName,
            false,
            Arg.Any<CancellationToken>());
    }
}
