using Bunit;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FishingLogBook.Web.Tests.Features.Diagnostics.Pages.OfflineDiagnosticsTests;

public class BaseOfflineDiagnosticsTest
{
    protected static BunitContext CreateContext(OfflineDiagnosticsSnapshotModel? snapshot = null)
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        context.JSInterop.Setup<OfflineDiagnosticsSnapshotModel>(
                "fishingLogBookDiagnostics.inspectOfflineStartup")
            .SetResult(snapshot ?? Snapshot());
        return context;
    }

    protected static OfflineDiagnosticsSnapshotModel Snapshot() => new()
    {
        DocumentBaseUri = "https://dev.test/",
        CurrentUrl = "https://dev.test/offline-diagnostics",
        ResolvedModuleUrl = "https://dev.test/js/browser/offline-access.js",
        ServiceWorkerSupported = true,
        ControllerPresent = true,
        ControllerScriptUrl = "https://dev.test/service-worker.js",
        ControllerCacheName = "offline-cache-v1",
        ControllerManifestVersion = "v1",
        ActiveWorkerState = "activated",
        ActiveWorkerScriptUrl = "https://dev.test/service-worker.js",
        CacheNames = ["offline-cache-v1"],
        MatchingCacheName = "offline-cache-v1",
        ModuleCached = true,
        ModuleContentType = "application/javascript",
        ModuleStatus = 200,
        ModuleRedirected = false,
        EntitlementDatabaseState = "found",
        EntitlementStorePresent = true,
        EntitlementRecordCount = 1,
        EntitlementRecordStates = ["ready"]
    };
}
