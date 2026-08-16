using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Pages.DiagnosticsInspector;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Diagnostics.Pages.DiagnosticsInspectorTests;

public class WhenTestingProbe : BaseDiagnosticsInspectorTest
{
    [Fact]
    public async Task ItShouldShowTheIsolatedProbeStage()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var probe = Substitute.For<IDiagnosticIndexedDbProbe>();
        probe.RunIsolatedAsync(Arg.Any<CancellationToken>())
            .Returns(new DiagnosticProbeResultModel
            {
                DatabaseName = DiagnosticIndexedDbProbe.IsolatedDatabaseName,
                LastCompletedStage = DiagnosticIndexedDbProbe.StageDatabaseOpened,
                FailedStage = DiagnosticIndexedDbProbe.StageWriting,
                Error = "TimeoutException"
            });
        var store = CreateStore();
        store.InspectExistingAsync(Arg.Any<CancellationToken>())
            .Returns(new DiagnosticDatabaseInspectionModel
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
                .Contain(DiagnosticIndexedDbProbe.StageDatabaseOpened);
            cut.Find("#diagnostics-probe-isolated-error").TextContent.Should()
                .Contain(DiagnosticIndexedDbProbe.StageWriting);
            cut.Find("#diagnostics-probe-production-stage").TextContent.Should()
                .Contain(DiagnosticIndexedDbProbe.StageCountReturned);
            cut.Find("#retry-diagnostics-probe-button").TextContent.Should()
                .Contain("Retry diagnostic probe");
        });
        await probe.Received(1).RunIsolatedAsync(Arg.Any<CancellationToken>());
        await store.Received(1).InspectExistingAsync(Arg.Any<CancellationToken>());
        await store.DidNotReceive().EnqueueAsync(Arg.Any<DiagnosticEventModel>(), Arg.Any<CancellationToken>());
    }
}
