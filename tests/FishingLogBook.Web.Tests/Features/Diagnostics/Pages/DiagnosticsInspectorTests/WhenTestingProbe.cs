using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Pages.DiagnosticsInspector;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Diagnostics.Pages.DiagnosticsInspectorTests;

public class WhenTestingProbe : BaseDiagnosticsInspectorTest
{
    [Fact]
    public async Task ItShouldShowTheProductionProbeStage()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = CreateStore();
        store.InspectExistingAsync(Arg.Any<CancellationToken>())
            .Returns(new DiagnosticDatabaseInspectionModel
            {
                Exists = true,
                HasStore = true,
                Count = 2
            });
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<DiagnosticsInspector>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#diagnostics-probe-production-stage").TextContent.Should()
                .Contain(DiagnosticsInspector.StageCountReturned);
            cut.Find("#retry-diagnostics-probe-button").TextContent.Should()
                .Contain("Retry diagnostic probe");
            cut.Find("#webauthn-capability-probe-link").GetAttribute("href").Should()
                .Be("/diagnostics/webauthn-capability-probe");
        });
        await store.Received(1).InspectExistingAsync(Arg.Any<CancellationToken>());
        await store.DidNotReceive().EnqueueAsync(Arg.Any<DiagnosticEventModel>(), Arg.Any<CancellationToken>());
    }
}
