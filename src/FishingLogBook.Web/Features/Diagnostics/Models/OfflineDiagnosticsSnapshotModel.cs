namespace FishingLogBook.Web.Features.Diagnostics.Models;

public sealed class OfflineDiagnosticsSnapshotModel
{
    public string DocumentBaseUri { get; set; } = string.Empty;
    public string CurrentUrl { get; set; } = string.Empty;
    public string ResolvedModuleUrl { get; set; } = string.Empty;
    public bool ServiceWorkerSupported { get; set; }
    public bool ControllerPresent { get; set; }
    public string? ControllerScriptUrl { get; set; }
    public string? ControllerCacheName { get; set; }
    public string? ControllerManifestVersion { get; set; }
    public string? ActiveWorkerState { get; set; }
    public string? ActiveWorkerScriptUrl { get; set; }
    public string? WaitingWorkerState { get; set; }
    public string? WaitingWorkerScriptUrl { get; set; }
    public string? InstallingWorkerState { get; set; }
    public string? InstallingWorkerScriptUrl { get; set; }
    public string[] CacheNames { get; set; } = [];
    public string? MatchingCacheName { get; set; }
    public bool ModuleCached { get; set; }
    public string? ModuleContentType { get; set; }
    public int? ModuleStatus { get; set; }
    public bool? ModuleRedirected { get; set; }
    public string EntitlementDatabaseState { get; set; } = string.Empty;
    public bool? EntitlementStorePresent { get; set; }
    public int? EntitlementRecordCount { get; set; }
    public string[] EntitlementRecordStates { get; set; } = [];
    public string? LastErrorSource { get; set; }
    public string? LastErrorType { get; set; }
    public string? LastErrorMessage { get; set; }
    public string? FailedStage { get; set; }
    public string? ErrorType { get; set; }
    public string? ErrorMessage { get; set; }
}
