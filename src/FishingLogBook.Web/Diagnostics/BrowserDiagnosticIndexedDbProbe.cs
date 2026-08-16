using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Configuration;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Diagnostics;

public sealed class BrowserDiagnosticIndexedDbProbe : IDiagnosticIndexedDbProbe
{
    public const string IsolatedDatabaseName = "FishingLogBookDiagnosticsTest";
    public const string IsolatedStoreName = "probeEvents";

    public const string StageStartingImport = "DIAG-01 starting module import";
    public const string StageModuleImported = "DIAG-02 module imported";
    public const string StageOpeningDatabase = "DIAG-03 opening diagnostic IndexedDB";
    public const string StageDatabaseOpened = "DIAG-04 database opened";
    public const string StageWriting = "DIAG-05 writing test diagnostic";
    public const string StageWriteCompleted = "DIAG-06 write transaction completed";
    public const string StageReadingCount = "DIAG-07 reading diagnostic count";
    public const string StageCountReturned = "DIAG-08 count returned";

    private const string ModulePath = "./js/diagnostic-probe.js";

    private readonly IJSRuntime _jsRuntime;
    private readonly DiagnosticsClientConfig _config;

    public BrowserDiagnosticIndexedDbProbe(IJSRuntime jsRuntime, DiagnosticsClientConfig config)
    {
        _jsRuntime = jsRuntime;
        _config = config;
    }

    public async Task<DiagnosticProbeResult> RunIsolatedAsync(CancellationToken cancellationToken)
    {
        var result = new DiagnosticProbeResult { DatabaseName = IsolatedDatabaseName };
        var timeoutMs = (int)_config.OperationTimeout.TotalMilliseconds;
        var inProgress = StageStartingImport;
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(_config.OperationTimeout);
            var token = timeoutSource.Token;

            var module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", token, ModulePath);
            result.LastCompletedStage = StageModuleImported;

            inProgress = StageOpeningDatabase;
            await module.InvokeVoidAsync("openProbeDatabase", token, timeoutMs);
            result.LastCompletedStage = StageDatabaseOpened;

            inProgress = StageWriting;
            await module.InvokeVoidAsync("writeProbeRecord", token, timeoutMs);
            result.LastCompletedStage = StageWriteCompleted;

            inProgress = StageReadingCount;
            result.Count = await module.InvokeAsync<int>("countProbeRecords", token, timeoutMs);
            result.LastCompletedStage = StageCountReturned;
            return result;
        }
        catch (Exception exception)
        {
            result.FailedStage = inProgress;
            result.Error = FormatError(exception);
            return result;
        }
    }

    private static string FormatError(Exception exception)
    {
        var message = DiagnosticMetadata.SafeErrorMessage(exception.Message, 120);
        return string.IsNullOrWhiteSpace(message)
            ? exception.GetType().Name
            : $"{exception.GetType().Name}: {message}";
    }
}
