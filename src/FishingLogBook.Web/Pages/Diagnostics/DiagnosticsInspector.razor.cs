using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Diagnostics;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Pages.Diagnostics;

public partial class DiagnosticsInspector : ComponentBase
{
    [Inject]
    private DiagnosticsClientConfig Config { get; set; } = default!;

    [Inject]
    private DiagnosticStatus Status { get; set; } = default!;

    [Inject]
    private IDiagnosticEventStore Store { get; set; } = default!;

    [Inject]
    private IDiagnosticSynchroniser Synchroniser { get; set; } = default!;

    [Inject]
    private IDiagnosticIndexedDbProbe Probe { get; set; } = default!;

    [Inject]
    private INetworkStatus NetworkStatus { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private IReadOnlyList<DiagnosticEvent> _events = [];
    private int _queuedCount;
    private bool _queueCountAvailable;
    private bool _eventsUnavailable;
    private string? _lastError;
    private string? _lastOperation;
    private string _usageLabel = "-";
    private string _quotaLabel = "-";
    private bool? _isOnline;
    private bool _isLoading;
    private DiagnosticProbeResult? _isolatedProbe;
    private DiagnosticProbeResult? _productionProbe;

    private string OnlineLabel => _isOnline switch
    {
        true => Loc["Diagnostics_OnlineYes"],
        false => Loc["Diagnostics_OnlineNo"],
        _ => Loc["Diagnostics_Unknown"]
    };

    private string QueueCountLabel => _queueCountAvailable
        ? $"{Loc["Diagnostics_QueuedCount"]}: {_queuedCount}"
        : Loc["Diagnostics_QueueUnavailable"];

    private bool ShowEmptyQueue => _queueCountAvailable && !_eventsUnavailable && _events.Count == 0;

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _isLoading = true;
        try
        {
            await RunProbesAsync();
            await SafeSynchroniseAsync();
            await ReadQueueAsync();
            await ReadOnlineAndStorageAsync();
            await ApplyStatusLabelsAsync();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task RetryProbesAsync()
    {
        _isLoading = true;
        try
        {
            await RunProbesAsync();
            await ApplyStatusLabelsAsync();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task RunProbesAsync()
    {
        _isolatedProbe = await Probe.RunAsync(
            BrowserDiagnosticIndexedDbProbe.IsolatedDatabaseName,
            true,
            CancellationToken.None);
        _productionProbe = await Probe.RunAsync(
            BrowserDiagnosticIndexedDbProbe.ProductionDatabaseName,
            false,
            CancellationToken.None);
    }

    private async Task ReadQueueAsync()
    {
        _queueCountAvailable = false;
        _eventsUnavailable = false;
        try
        {
            Status.RecordSuccess(DiagnosticOperations.QueueCount);
            _queuedCount = await Store.GetCountAsync(CancellationToken.None);
            Status.RecordQueueCount(_queuedCount);
            _queueCountAvailable = true;
        }
        catch (Exception exception)
        {
            Status.MarkQueueCountUnavailable();
            Status.RecordFailure(DiagnosticOperations.QueueCount, exception);
            TryToLogError("diagnostics queue count", exception);
            _eventsUnavailable = true;
            return;
        }

        try
        {
            Status.RecordSuccess(DiagnosticOperations.QueueRead);
            _events = await Store.GetPendingAsync(Config.MaxQueueSize, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _events = [];
            _eventsUnavailable = true;
            Status.RecordFailure(DiagnosticOperations.QueueRead, exception);
            TryToLogError("diagnostics queue read", exception);
        }
    }

    private async Task ReadOnlineAndStorageAsync()
    {
        try
        {
            _isOnline = await NetworkStatus.IsOnlineAsync(CancellationToken.None);
            Status.IsOnline = _isOnline;
        }
        catch (Exception exception)
        {
            Status.RecordFailure(DiagnosticOperations.NetworkCheck, exception);
            TryToLogError("diagnostics online", exception);
        }

        try
        {
            var estimate = await Store.GetStorageEstimateAsync(CancellationToken.None);
            _usageLabel = estimate.Usage?.ToString() ?? Loc["Diagnostics_UnavailableValue"];
            _quotaLabel = estimate.Quota?.ToString() ?? Loc["Diagnostics_UnavailableValue"];
            Status.StorageUsageBytes = estimate.Usage;
            Status.StorageQuotaBytes = estimate.Quota;
        }
        catch (Exception exception)
        {
            _usageLabel = Loc["Diagnostics_UnavailableValue"];
            _quotaLabel = Loc["Diagnostics_UnavailableValue"];
            Status.RecordFailure(DiagnosticOperations.Refresh, exception);
            TryToLogError("diagnostics storage", exception);
        }
    }

    private async Task SafeSynchroniseAsync()
    {
        try
        {
            await Synchroniser.SynchronisePendingAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            Status.RecordFailure(DiagnosticOperations.Upload, exception);
            TryToLogError("diagnostic upload", exception);
        }
    }

    private async Task ApplyStatusLabelsAsync()
    {
        _lastOperation = Status.LastOperation;
        if (_queueCountAvailable)
        {
            _queuedCount = Status.QueuedCount;
        }

        _lastError = Status.LastError ?? await ReadLoggedErrorAsync();
    }

    private async Task<string?> ReadLoggedErrorAsync()
    {
        var lastError = await Logging.GetLastErrorAsync();
        if (lastError is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(lastError.ErrorType))
        {
            return lastError.Message;
        }

        return $"{lastError.ErrorType}: {lastError.Message}";
    }

    private void TryToLogError(string source, Exception exception)
    {
        _ = Logging.LogErrorAsync(source, exception);
    }
}
