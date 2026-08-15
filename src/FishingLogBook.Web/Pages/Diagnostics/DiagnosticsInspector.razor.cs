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
    private INetworkStatus NetworkStatus { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private IReadOnlyList<DiagnosticEvent> _events = [];
    private int _queuedCount;
    private string? _lastError;
    private string _usageLabel = "-";
    private string _quotaLabel = "-";
    private bool? _isOnline;
    private bool _isLoading;

    private string OnlineLabel => _isOnline switch
    {
        true => Loc["Diagnostics_OnlineYes"],
        false => Loc["Diagnostics_OnlineNo"],
        _ => Loc["Diagnostics_Unknown"]
    };

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _isLoading = true;
        try
        {
            await SafeSynchroniseAsync();
            _queuedCount = await Store.GetCountAsync(CancellationToken.None);
            _events = await Store.GetPendingAsync(Config.MaxQueueSize, CancellationToken.None);
            _lastError = await ReadLastErrorAsync();
            _isOnline = await NetworkStatus.IsOnlineAsync(CancellationToken.None);
            var estimate = await Store.GetStorageEstimateAsync(CancellationToken.None);
            _usageLabel = estimate.Usage?.ToString() ?? Loc["Diagnostics_UnavailableValue"];
            _quotaLabel = estimate.Quota?.ToString() ?? Loc["Diagnostics_UnavailableValue"];
            Status.QueuedCount = _queuedCount;
            Status.IsOnline = _isOnline;
            Status.StorageUsageBytes = estimate.Usage;
            Status.StorageQuotaBytes = estimate.Quota;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("diagnostics refresh", exception);
            _lastError = await ReadLastErrorAsync() ?? exception.GetType().Name;
            Status.LastError = exception.GetType().Name;
        }
        finally
        {
            _isLoading = false;
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
            await Logging.LogErrorAsync("diagnostic upload", exception);
            Status.LastError = exception.GetType().Name;
        }
    }

    private async Task<string?> ReadLastErrorAsync()
    {
        var lastError = await Logging.GetLastErrorAsync();
        if (lastError is null)
        {
            return Status.LastError;
        }

        if (string.IsNullOrWhiteSpace(lastError.ErrorType))
        {
            return lastError.Message;
        }

        return $"{lastError.ErrorType}: {lastError.Message}";
    }
}
