using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Features.SystemStatus.Clients;
using FishingLogBook.Web.Features.SystemStatus.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.SystemStatus.Pages.SystemStatus;

public partial class SystemStatus : ComponentBase, IDisposable
{
    private const string HealthyStatus = "Healthy";
    private const string DegradedStatus = "Degraded";

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private StatusState _webStatus = StatusState.Online;
    private StatusState _apiStatus = StatusState.Checking;
    private StatusState _databaseStatus = StatusState.Checking;
    private string? _databaseName;
    private bool _isChecking;

    [Inject]
    private ISystemStatusClient SystemStatusClient { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _isChecking = true;
        _apiStatus = StatusState.Checking;
        _databaseStatus = StatusState.Checking;
        _databaseName = null;

        _apiStatus = await CheckApiAsync();

        if (_apiStatus == StatusState.Online)
        {
            _databaseStatus = await CheckDatabaseAsync();
        }
        else
        {
            _databaseStatus = StatusState.Offline;
        }

        _isChecking = false;
    }

    private async Task<StatusState> CheckApiAsync()
    {
        try
        {
            var health = await SystemStatusClient.GetApiHealthAsync(_cancellationTokenSource.Token);
            return health is not null && health.Status == HealthyStatus
                ? StatusState.Online
                : StatusState.Offline;
        }
        catch (Exception)
        {
            return StatusState.Offline;
        }
    }

    private async Task<StatusState> CheckDatabaseAsync()
    {
        try
        {
            var database = await SystemStatusClient.GetDatabaseStatusAsync(_cancellationTokenSource.Token);

            if (database is null)
            {
                return StatusState.Offline;
            }

            _databaseName = database.Name;

            return database.Status switch
            {
                HealthyStatus => StatusState.Online,
                DegradedStatus => StatusState.Degraded,
                _ => StatusState.Offline
            };
        }
        catch (Exception)
        {
            return StatusState.Offline;
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
