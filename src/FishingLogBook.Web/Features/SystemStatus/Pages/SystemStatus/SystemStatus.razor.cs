using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Browser.Update;
using FishingLogBook.Web.Configuration;
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
    private BuildMetadataDto? _apiBuild;
    private bool _isChecking;

    [Inject]
    private ISystemStatusClient SystemStatusClient { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    [Inject]
    private BuildMetadataConfig WebBuild { get; set; } = default!;

    [Inject]
    private IAppUpdateService AppUpdate { get; set; } = default!;

    private bool CanUpdate => AppUpdate.Status is AppUpdateStatus.Available or AppUpdateStatus.Failed;

    private string UpdateStatusText
    {
        get
        {
            return AppUpdate.Status switch
            {
                AppUpdateStatus.Current => Loc["Update_StatusCurrent"],
                AppUpdateStatus.Activating => Loc["Update_ActivatingTitle"],
                AppUpdateStatus.Failed => Loc["Update_FailedTitle"],
                _ => Loc["Update_StatusAvailable"]
            };
        }
    }

    protected override async Task OnInitializedAsync()
    {
        AppUpdate.StatusChanged += OnUpdateStatusChanged;
        await RefreshAsync();
    }

    private async Task UpdateAsync()
    {
        await AppUpdate.ApplyAsync(_cancellationTokenSource.Token);
    }

    private void OnUpdateStatusChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    private async Task RefreshAsync()
    {
        _isChecking = true;
        _apiStatus = StatusState.Checking;
        _databaseStatus = StatusState.Checking;
        _databaseName = null;
        _apiBuild = null;

        _apiStatus = await CheckApiAsync();

        if (_apiStatus == StatusState.Online)
        {
            _apiBuild = await GetApiBuildAsync();
            _databaseStatus = await CheckDatabaseAsync();
        }
        else
        {
            _databaseStatus = StatusState.Offline;
        }

        _isChecking = false;
    }

    private async Task<BuildMetadataDto?> GetApiBuildAsync()
    {
        try
        {
            return await SystemStatusClient.GetBuildMetadataAsync(_cancellationTokenSource.Token);
        }
        catch (Exception)
        {
            return null;
        }
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
        AppUpdate.StatusChanged -= OnUpdateStatusChanged;
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
