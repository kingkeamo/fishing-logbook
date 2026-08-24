using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor.Services;
using OfflineDiagnosticsPage = FishingLogBook.Web.Features.Diagnostics.Pages.OfflineDiagnostics.OfflineDiagnostics;

namespace FishingLogBook.Web.Tests.Features.Diagnostics.Pages.OfflineDiagnosticsTests;

public class WhenTestingRender : BaseOfflineDiagnosticsTest
{
    [Fact]
    public async Task ItShouldShowLocalReadOnlyStartupEvidence()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<OfflineDiagnosticsPage>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#offline-diagnostics-results").TextContent.Should()
            .Contain("offline-cache-v1")
            .And.Contain("application/javascript")
            .And.Contain("ready"));
        context.JSInterop.VerifyInvoke("fishingLogBookDiagnostics.inspectOfflineStartup", 1);
    }

    [Fact]
    public async Task ItShouldKeepThePageUsableWhenInspectionInteropFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.JSInterop.Setup<OfflineDiagnosticsSnapshotModel>(
                "fishingLogBookDiagnostics.inspectOfflineStartup")
            .SetException(new JSException("inspection failed"));

        // Act
        var cut = context.Render<OfflineDiagnosticsPage>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#offline-diagnostics-results").TextContent.Should()
            .Contain("diagnostics-interop")
            .And.Contain("inspection failed"));
        context.JSInterop.VerifyInvoke("fishingLogBookDiagnostics.inspectOfflineStartup", 1);
    }
}
