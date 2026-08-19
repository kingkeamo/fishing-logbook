using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Modals.LocationPrivacy;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Pages.CatchList;

public partial class CatchList : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private IReadOnlyList<CatchModel> _catches = [];
    private readonly HashSet<Guid> _retrying = [];
    private bool _isLoading = true;
    private bool _loadFailed;

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private ILocalCatchOwnerService LocalCatchOwner { get; set; } = default!;

    [Inject]
    private ICatchSynchroniser CatchSynchroniser { get; set; } = default!;

    [Inject]
    private IModalService ModalService { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        CatchSynchroniser.StateChanged += OnSyncStateChanged;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadFailed = false;
        try
        {
            var ownerUserId = await LocalCatchOwner.GetUserIdAsync(_cancellationTokenSource.Token);
            var saved = await CatchStore.GetAllAsync(ownerUserId, _cancellationTokenSource.Token);
            _catches = saved
                .OrderByDescending(catchRecord => catchRecord.CaughtOn)
                .ToArray();
        }
        catch (Exception)
        {
            _loadFailed = true;
            _catches = [];
        }
        finally
        {
            _isLoading = false;
        }
    }

    private string SpeciesLabel(string? speciesName)
    {
        return string.IsNullOrWhiteSpace(speciesName)
            ? Loc["Catch_UnknownSpecies"]
            : speciesName;
    }

    private static string ThumbnailUrl(CatchPhotographModel photograph)
    {
        return $"data:{photograph.ContentType};base64,{Convert.ToBase64String(photograph.Bytes!)}";
    }

    private async Task OpenLocationPrivacyAsync(Guid catchId)
    {
        var result = await ModalService.ShowAsync<LocationPrivacyModal, LocationPrivacyModalModel, LocationPrivacyModalResult>(
            new LocationPrivacyModalModel(catchId),
            _cancellationTokenSource.Token);
        if (result?.Saved == true)
        {
            await LoadAsync();
        }
    }

    private async Task RetryAsync(Guid catchId)
    {
        if (!_retrying.Add(catchId))
        {
            return;
        }

        try
        {
            await CatchSynchroniser.RetryAsync(catchId, _cancellationTokenSource.Token);
            await LoadAsync();
        }
        finally
        {
            _retrying.Remove(catchId);
        }
    }

    private void OnSyncStateChanged(object? sender, EventArgs args)
    {
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }

        try
        {
            _ = InvokeAsync(RefreshAfterSynchronisationAsync);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task RefreshAfterSynchronisationAsync()
    {
        await LoadAsync();
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }

        StateHasChanged();
    }

    private static string SyncStatusKey(SyncStatus syncStatus)
    {
        return syncStatus switch
        {
            SyncStatus.SavedLocally => "SyncStatus_SavedLocally",
            SyncStatus.WaitingToSynchronise => "SyncStatus_WaitingToSynchronise",
            SyncStatus.Synchronising => "SyncStatus_Synchronising",
            SyncStatus.Synchronised => "SyncStatus_Synchronised",
            SyncStatus.FailedToSynchronise => "SyncStatus_FailedToSynchronise",
            _ => "SyncStatus_SavedLocally"
        };
    }

    private static string SyncStatusIcon(SyncStatus syncStatus)
    {
        return syncStatus switch
        {
            SyncStatus.Synchronised => MudBlazor.Icons.Material.Filled.CheckCircle,
            SyncStatus.FailedToSynchronise => MudBlazor.Icons.Material.Filled.SyncProblem,
            SyncStatus.Synchronising => MudBlazor.Icons.Material.Filled.Sync,
            _ => MudBlazor.Icons.Material.Filled.CloudQueue
        };
    }

    public void Dispose()
    {
        CatchSynchroniser.StateChanged -= OnSyncStateChanged;
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
