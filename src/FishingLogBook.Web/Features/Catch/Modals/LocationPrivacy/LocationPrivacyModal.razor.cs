using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
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
    private bool _queueFailed;

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
    private ILoggingService Logging { get; set; } = default!;

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
            _catch = await CatchStore.GetAsync(ownerUserId, CatchId, _cancellationTokenSource.Token);
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
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("loading catch location privacy", exception, CancellationToken.None);
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
        _queueFailed = false;
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
        _queueFailed = false;
        try
        {
            if (!await TryPersistLocalVisibilityAsync())
            {
                return;
            }

            _savedOnDevice = true;
            await InvokeAsync(StateHasChanged);
            if (!await PropagateVisibilityAsync())
            {
                _queueFailed = true;
                return;
            }

            await Task.Delay(SavedFeedbackDelay, _cancellationTokenSource.Token);
            MudDialog.Close(DialogResult.Ok(new LocationPrivacyModalResult(true)));
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("saving catch location privacy", exception, CancellationToken.None);
            if (!_savedOnDevice)
            {
                _saveFailed = true;
            }
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
                Location = _catch.Location! with { Visibility = _visibility }
            };
            await CatchStore.SaveAsync(updated, _cancellationTokenSource.Token);
            _catch = updated;
            return true;
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("saving catch location privacy locally", exception, CancellationToken.None);
            _saveFailed = true;
            return false;
        }
    }

    private async Task<bool> PropagateVisibilityAsync()
    {
        try
        {
            await CatchClient.UpdateLocationVisibilityAsync(
                CatchId,
                _visibility,
                _cancellationTokenSource.Token);
            return true;
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsRecoverablePropagationFailure(exception))
        {
            await Logging.LogErrorAsync(
                "updating catch location visibility",
                exception,
                CancellationToken.None);
            return await PersistWaitingToSynchroniseAsync();
        }
    }

    private async Task<bool> PersistWaitingToSynchroniseAsync()
    {
        if (_catch is null)
        {
            return false;
        }

        if (_catch.SyncStatus != SyncStatus.Synchronised
            && _catch.MetadataSyncStatus != SyncStatus.Synchronised)
        {
            return true;
        }

        try
        {
            var waiting = _catch with
            {
                SyncStatus = _catch.SyncStatus == SyncStatus.Synchronised
                    ? SyncStatus.WaitingToSynchronise
                    : _catch.SyncStatus,
                MetadataSyncStatus = _catch.MetadataSyncStatus == SyncStatus.Synchronised
                    ? SyncStatus.WaitingToSynchronise
                    : _catch.MetadataSyncStatus
            };
            await CatchStore.SaveAsync(waiting, _cancellationTokenSource.Token);
            _catch = waiting;
            return true;
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "queueing location privacy for synchronisation",
                exception,
                CancellationToken.None);
            return false;
        }
    }

    private static bool IsRecoverablePropagationFailure(Exception exception)
    {
        return exception is HttpRequestException or TaskCanceledException or TimeoutException;
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
