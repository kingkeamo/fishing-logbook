using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Pages.CatchLocationPrivacy;

public partial class CatchLocationPrivacy : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private CatchModel? _catch;
    private string _visibility = LocationDefaults.Private;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _loadFailed;
    private bool _missingLocation;
    private bool _saveFailed;
    private bool _saved;
    private bool _savedPendingSync;

    [Parameter]
    public Guid CatchId { get; set; }

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private ICatchClient CatchClient { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

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
            var saved = await CatchStore.GetAllAsync(_cancellationTokenSource.Token);
            _catch = saved.FirstOrDefault(catchRecord => catchRecord.Id == CatchId);
            if (_catch?.Location is null)
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
        _saved = false;
        _savedPendingSync = false;
        _saveFailed = false;
    }

    private async Task SaveAsync()
    {
        if (_catch?.Location is null || _isSaving)
        {
            return;
        }

        _isSaving = true;
        _saveFailed = false;
        _saved = false;
        _savedPendingSync = false;
        try
        {
            if (!await TryPersistLocalVisibilityAsync())
            {
                return;
            }

            await PropagateVisibilityAsync();
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
            _saved = true;
        }
        catch (HttpRequestException)
        {
            _savedPendingSync = true;
        }
        catch (TaskCanceledException)
        {
            _savedPendingSync = true;
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
