using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Features.Catch.Modals.LocationPrivacy;

public partial class LocationPrivacyModal : ComponentBase, IDisposable
{
    private static readonly TimeSpan SavedFeedbackDelay = TimeSpan.FromMilliseconds(500);

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private CatchModel? _catch;
    private string _visibility = LocationDefaults.Private;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _loadFailed;
    private bool _missingLocation;
    private bool _saveFailed;
    private bool _savedOnDevice;
    private bool _restoreCatchSync;
    private bool _restoreMetadataSync;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public LocationPrivacyModalModel Model { get; set; } = default!;

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private ICatchClient CatchClient { get; set; } = default!;

    [Inject]
    private ILocalCatchOwnerService LocalCatchOwner { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private Guid CatchId => Model.CatchId;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadFailed = false;
        _missingLocation = false;
        try
        {
            var ownerUserId = await LocalCatchOwner.GetUserIdAsync(_cancellationTokenSource.Token);
            var saved = await CatchStore.GetAllAsync(ownerUserId, _cancellationTokenSource.Token);
            _catch = saved.FirstOrDefault(catchRecord => catchRecord.Id == CatchId);
            if (_catch is null)
            {
                _loadFailed = true;
                return;
            }

            if (_catch.Location is null)
            {
                _missingLocation = true;
                return;
            }

            _visibility = _catch.Location.Visibility;
        }
        catch (Exception)
        {
            _loadFailed = true;
            _catch = null;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void OnVisibilityChanged(string visibility)
    {
        _visibility = visibility;
        _saveFailed = false;
        _savedOnDevice = false;
    }

    private async Task SaveAsync()
    {
        if (_catch?.Location is null || _isSaving)
        {
            return;
        }

        _isSaving = true;
        _saveFailed = false;
        _savedOnDevice = false;
        try
        {
            if (!await TryPersistLocalVisibilityAsync())
            {
                return;
            }

            _savedOnDevice = true;
            await InvokeAsync(StateHasChanged);
            await PropagateVisibilityAsync();
            await Task.Delay(SavedFeedbackDelay, _cancellationTokenSource.Token);
            MudDialog.Close(DialogResult.Ok(new LocationPrivacyModalResult(true)));
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task<bool> TryPersistLocalVisibilityAsync()
    {
        try
        {
            var updated = _catch! with
            {
                Location = _catch.Location! with { Visibility = _visibility },
                SyncStatus = _catch.SyncStatus == SyncStatus.Synchronised
                    ? SyncStatus.WaitingToSynchronise
                    : _catch.SyncStatus,
                MetadataSyncStatus = _catch.MetadataSyncStatus == SyncStatus.Synchronised
                    ? SyncStatus.WaitingToSynchronise
                    : _catch.MetadataSyncStatus
            };
            _restoreCatchSync = _catch.SyncStatus == SyncStatus.Synchronised;
            _restoreMetadataSync = _catch.MetadataSyncStatus == SyncStatus.Synchronised;
            await CatchStore.SaveAsync(updated, _cancellationTokenSource.Token);
            _catch = updated;
            return true;
        }
        catch (Exception)
        {
            _saveFailed = true;
            return false;
        }
    }

    private async Task PropagateVisibilityAsync()
    {
        try
        {
            await CatchClient.UpdateLocationVisibilityAsync(
                CatchId,
                _visibility,
                _cancellationTokenSource.Token);
            await RestoreSynchronisedAfterSuccessfulPropagateAsync();
        }
        catch (HttpRequestException)
        {
            return;
        }
        catch (TaskCanceledException)
        {
            return;
        }
    }

    private async Task RestoreSynchronisedAfterSuccessfulPropagateAsync()
    {
        if (_catch is null || (!_restoreCatchSync && !_restoreMetadataSync))
        {
            return;
        }

        var restored = _catch with
        {
            SyncStatus = _restoreCatchSync ? SyncStatus.Synchronised : _catch.SyncStatus,
            MetadataSyncStatus = _restoreMetadataSync ? SyncStatus.Synchronised : _catch.MetadataSyncStatus
        };
        await CatchStore.SaveAsync(restored, _cancellationTokenSource.Token);
        _catch = restored;
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
