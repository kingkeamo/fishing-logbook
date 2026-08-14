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
    private INetworkStatus NetworkStatus { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private int _queuedCount;
    private string? _lastError;
    private string _usageLabel = "-";
    private string _quotaLabel = "-";
    private bool? _isOnline;

    private string OnlineLabel => _isOnline switch
    {
        true => Loc["Diagnostics_OnlineYes"],
        false => Loc["Diagnostics_OnlineNo"],
        _ => Loc["Diagnostics_Unknown"]
    };

    protected override async Task OnInitializedAsync()
    {
        if (!Config.ShowInspector)
        {
            return;
        }

        try
        {
            _queuedCount = await Store.GetCountAsync(CancellationToken.None);
            _lastError = Status.LastError;
            _isOnline = await NetworkStatus.IsOnlineAsync(CancellationToken.None);
            var estimate = await Store.GetStorageEstimateAsync(CancellationToken.None);
            _usageLabel = estimate.Usage?.ToString() ?? Loc["Diagnostics_UnavailableValue"];
            _quotaLabel = estimate.Quota?.ToString() ?? Loc["Diagnostics_UnavailableValue"];
            Status.QueuedCount = _queuedCount;
            Status.IsOnline = _isOnline;
            Status.StorageUsageBytes = estimate.Usage;
            Status.StorageQuotaBytes = estimate.Quota;
        }
        catch
        {
            _lastError = Status.LastError ?? Loc["Diagnostics_Unknown"];
        }
    }
}
