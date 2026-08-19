using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage;
using FishingLogBook.Web.Features.Diagnostics.Synchronisers;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;
using FishingLogBook.Web.Features.TestCatch.Offline.Stores;
using FishingLogBook.Web.Features.TestCatch.Offline.Synchronisers;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using MudBlazor;

namespace FishingLogBook.Web.Features.TestCatch.Pages.TestCatchLog;

public partial class TestCatchLog : ComponentBase, IDisposable
{
    private const long MaxPhotographBytes = 10 * 1024 * 1024;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Dictionary<Guid, string> _photographUrls = [];
    private DotNetObjectReference<TestCatchLog>? _dotNetReference;

    private string _speciesName = string.Empty;
    private string _notes = string.Empty;
    private IReadOnlyList<TestCatchModel> _catches = [];
    private bool _isSaving;
    private bool _isLoading;
    private bool _loadFailed;
    private byte[]? _pendingPhotographBytes;
    private string? _pendingPhotographContentType;
    private string? _pendingPhotographUrl;
    private LocationPromptStatus _locationPrompt = new(false, false, false);

    [Inject]
    private ITestCatchStore TestCatchStore { get; set; } = default!;

    [Inject]
    private ITestCatchPhotoStore TestCatchPhotoStore { get; set; } = default!;

    [Inject]
    private ITestCatchSynchroniser TestCatchSynchroniser { get; set; } = default!;

    [Inject]
    private IDiagnosticLogger Diagnostics { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IDiagnosticSynchroniser DiagnosticSynchroniser { get; set; } = default!;

    [Inject]
    private CorrelationContext CorrelationContext { get; set; } = default!;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    [Inject]
    private ILocationService LocationService { get; set; } = default!;

    private bool CanSave => !_isSaving && !string.IsNullOrWhiteSpace(_speciesName);

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        TryToRefreshLocationPrompt();
        await TestCatchSynchroniser.SynchronisePendingAsync(_cancellationTokenSource.Token);
        TryToSynchroniseDiagnostics();
        await LoadAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        _dotNetReference = DotNetObjectReference.Create(this);
        await JsRuntime.InvokeVoidAsync("fishingLogBookNetwork.onOnline", _dotNetReference);
    }

    [JSInvokable]
    public async Task OnBrowserOnline()
    {
        await TestCatchSynchroniser.SynchronisePendingAsync(_cancellationTokenSource.Token);
        TryToSynchroniseDiagnostics();
        await LoadAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnPhotographSelected(InputFileChangeEventArgs args)
    {
        var file = args.File;
        await using var stream = file.OpenReadStream(MaxPhotographBytes);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, _cancellationTokenSource.Token);
        _pendingPhotographBytes = buffer.ToArray();
        _pendingPhotographContentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "image/jpeg"
            : file.ContentType;
        _pendingPhotographUrl = $"data:{_pendingPhotographContentType};base64,{Convert.ToBase64String(_pendingPhotographBytes)}";
    }

    private async Task SaveAsync()
    {
        if (!CanSave)
        {
            return;
        }

        _isSaving = true;
        await InvokeAsync(StateHasChanged);
        CorrelationContext.StartNew();
        var saved = false;
        try
        {
            saved = await PersistNewCatchAsync();
        }
        finally
        {
            _isSaving = false;
        }

        if (!saved)
        {
            return;
        }

        await InvokeAsync(StateHasChanged);
        await TryToReloadLocalCatchesAsync();
        TryToSynchroniseCatches();
        TryToSynchroniseDiagnostics();
    }

    private async Task<bool> PersistNewCatchAsync()
    {
        TryToLog(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.CatchOfflineSaveStarted,
            "Catch offline save started.");

        var photograph = CreatePendingPhotograph();
        var location = await TryCaptureLocationAsync();
        var testCatch = new TestCatchModel(
            Guid.NewGuid(),
            _speciesName.Trim(),
            DateTimeOffset.UtcNow,
            string.IsNullOrWhiteSpace(_notes) ? null : _notes.Trim(),
            SyncStatus.SavedLocally,
            photograph,
            location);

        try
        {
            await TestCatchStore.SaveAsync(testCatch, _cancellationTokenSource.Token);
            await PersistPendingPhotographAsync(testCatch.Id, photograph);
            TryToLog(
                DiagnosticLevel.Warning,
                DiagnosticEventNames.CatchOfflineSaveCompleted,
                "Catch offline save completed.");
            ClearForm();
            return true;
        }
        catch (Exception exception)
        {
            TryToLog(
                DiagnosticLevel.Error,
                DiagnosticEventNames.CatchOfflineSaveFailed,
                "Catch offline save failed.",
                exception);
            return false;
        }
    }

    private TestCatchPhotographModel? CreatePendingPhotograph()
    {
        if (_pendingPhotographBytes is not { Length: > 0 } || _pendingPhotographContentType is null)
        {
            return null;
        }

        return new TestCatchPhotographModel(
            Guid.NewGuid(),
            _pendingPhotographContentType,
            SyncStatus.SavedLocally);
    }

    private async Task PersistPendingPhotographAsync(Guid testCatchId, TestCatchPhotographModel? photograph)
    {
        if (photograph is null || _pendingPhotographBytes is null)
        {
            return;
        }

        await TestCatchPhotoStore.PutAsync(
            testCatchId,
            _pendingPhotographBytes,
            photograph.ContentType,
            _cancellationTokenSource.Token);
    }

    private void ClearForm()
    {
        _speciesName = string.Empty;
        _notes = string.Empty;
        _pendingPhotographBytes = null;
        _pendingPhotographContentType = null;
        _pendingPhotographUrl = null;
    }

    private async Task AllowLocationAsync()
    {
        try
        {
            await LocationService
                .TryCaptureAsync(true, _cancellationTokenSource.Token)
                .WaitAsync(
                    TimeSpan.FromMilliseconds(Browser.Location.LocationService.CaptureTimeoutMilliseconds),
                    _cancellationTokenSource.Token);
        }
        catch (Exception exception)
        {
            TryToLog(
                DiagnosticLevel.Warning,
                DiagnosticEventNames.LocationCaptureFailed,
                "Location capture failed.",
                exception);
        }

        await RefreshLocationPromptAsync();
    }

    private async Task DismissLocationAsync()
    {
        try
        {
            await LocationService.DismissPromptAsync(_cancellationTokenSource.Token);
        }
        catch (Exception exception)
        {
            TryToLogError("location prompt", exception);
        }

        await RefreshLocationPromptAsync();
    }

    private async Task RemoveLocationAsync(Guid id)
    {
        var testCatch = _catches.FirstOrDefault(item => item.Id == id);
        if (testCatch?.Location is null)
        {
            return;
        }

        await TestCatchStore.SaveAsync(
            testCatch with { Location = null, SyncStatus = SyncStatus.SavedLocally },
            _cancellationTokenSource.Token);
        await LoadAsync();
        await TestCatchSynchroniser.SynchronisePendingAsync(_cancellationTokenSource.Token);
    }

    private async Task RefreshLocationPromptAsync()
    {
        try
        {
            _locationPrompt = await LocationService.GetPromptStatusAsync(_cancellationTokenSource.Token)
                .WaitAsync(
                    TimeSpan.FromMilliseconds(Browser.Location.LocationService.PermissionQueryTimeoutMilliseconds),
                    _cancellationTokenSource.Token);
        }
        catch (Exception exception)
        {
            _locationPrompt = new LocationPromptStatus(false, true, false);
            TryToLogError("location prompt", exception);
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task<CatchLocationModel?> TryCaptureLocationAsync()
    {
        try
        {
            return await LocationService
                .TryCaptureAsync(false, _cancellationTokenSource.Token)
                .WaitAsync(
                    TimeSpan.FromMilliseconds(Browser.Location.LocationService.CaptureTimeoutMilliseconds),
                    _cancellationTokenSource.Token);
        }
        catch (Exception exception)
        {
            TryToLog(
                DiagnosticLevel.Warning,
                DiagnosticEventNames.LocationCaptureFailed,
                "Location capture failed.",
                exception);
            return null;
        }
    }

    private async Task RetryAsync(Guid id)
    {
        await TestCatchSynchroniser.RetryAsync(id, _cancellationTokenSource.Token);
        await LoadAsync();
    }

    private async Task RetryPhotographAsync(Guid id)
    {
        await TestCatchSynchroniser.RetryPhotographAsync(id, _cancellationTokenSource.Token);
        await LoadAsync();
    }

    private async Task RetryLoadAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        try
        {
            TryToLog(
                DiagnosticLevel.Warning,
                DiagnosticEventNames.CatchOfflineLoadStarted,
                "Catch offline load started.");

            try
            {
                _catches = (await TestCatchStore.GetAllAsync(_cancellationTokenSource.Token))
                    .OrderByDescending(testCatch => testCatch.CaughtOn)
                    .ToArray();
                _loadFailed = false;
            }
            catch (Exception exception)
            {
                _loadFailed = true;
                TryToLog(
                    DiagnosticLevel.Error,
                    DiagnosticEventNames.OfflineDbReadFailed,
                    "Loading local catches failed.",
                    exception);
                return;
            }

            await LoadPhotographsAsync();
            TryToLog(
                DiagnosticLevel.Warning,
                DiagnosticEventNames.CatchOfflineLoadCompleted,
                "Catch offline load completed.");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadPhotographsAsync()
    {
        _photographUrls.Clear();
        var photographFailed = false;
        foreach (var testCatch in _catches)
        {
            if (!await TryLoadPhotographAsync(testCatch))
            {
                photographFailed = true;
            }
        }

        if (photographFailed)
        {
            _loadFailed = true;
        }
    }

    private async Task<bool> TryLoadPhotographAsync(TestCatchModel testCatch)
    {
        try
        {
            var local = await TestCatchPhotoStore.GetAsync(testCatch.Id, _cancellationTokenSource.Token);
            if (local is not null)
            {
                _photographUrls[testCatch.Id] = $"data:{local.ContentType};base64,{Convert.ToBase64String(local.Bytes)}";
                return true;
            }

            ApplyRemotePhotograph(testCatch);
            return true;
        }
        catch (Exception exception)
        {
            TryToLog(
                DiagnosticLevel.Error,
                DiagnosticEventNames.OfflineDbReadFailed,
                "Loading local photograph failed.",
                exception);
            ApplyRemotePhotograph(testCatch);
            return false;
        }
    }

    private void ApplyRemotePhotograph(TestCatchModel testCatch)
    {
        if (!string.IsNullOrWhiteSpace(testCatch.Photograph?.RemoteUrl))
        {
            _photographUrls[testCatch.Id] = testCatch.Photograph.RemoteUrl;
        }
    }

    private void TryToRefreshLocationPrompt()
    {
        _ = RefreshLocationPromptAsync();
    }

    private void TryToSynchroniseCatches()
    {
        _ = SafeCatchSyncAsync();
    }

    private void TryToSynchroniseDiagnostics()
    {
        _ = SafeDiagnosticSyncAsync();
    }

    private async Task TryToReloadLocalCatchesAsync()
    {
        try
        {
            await LoadAsync();
        }
        catch (Exception exception)
        {
            TryToLogError("catch list reload", exception);
        }
    }

    private async Task SafeCatchSyncAsync()
    {
        try
        {
            await TestCatchSynchroniser.SynchronisePendingAsync(_cancellationTokenSource.Token);
        }
        catch (Exception exception)
        {
            TryToLogError("catch sync", exception);
        }

        await TryToReloadLocalCatchesAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task SafeDiagnosticSyncAsync()
    {
        try
        {
            await DiagnosticSynchroniser.SynchronisePendingAsync(_cancellationTokenSource.Token);
        }
        catch (Exception exception)
        {
            TryToLogError("diagnostic upload", exception);
        }
    }

    private void TryToLog(
        DiagnosticLevel level,
        string eventName,
        string message,
        Exception? exception = null)
    {
        _ = SafeLogAsync(level, eventName, message, exception);
    }

    private void TryToLogError(string source, Exception exception)
    {
        _ = Logging.LogErrorAsync(source, exception, _cancellationTokenSource.Token);
    }

    private async Task SafeLogAsync(
        DiagnosticLevel level,
        string eventName,
        string message,
        Exception? exception = null)
    {
        try
        {
            await Diagnostics.LogAsync(
                level,
                eventName,
                message,
                exception: exception,
                cancellationToken: _cancellationTokenSource.Token);
        }
        catch (Exception loggingException)
        {
            TryToLogError("diagnostic log", loggingException);
        }
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

    private static string PhotoStatusKey(SyncStatus syncStatus)
    {
        return syncStatus switch
        {
            SyncStatus.SavedLocally => "TestCatch_PhotoSavedLocally",
            SyncStatus.WaitingToSynchronise => "TestCatch_PhotoWaiting",
            SyncStatus.Synchronising => "TestCatch_PhotoUploading",
            SyncStatus.Synchronised => "TestCatch_PhotoUploaded",
            SyncStatus.FailedToSynchronise => "TestCatch_PhotoFailed",
            _ => "TestCatch_PhotoSavedLocally"
        };
    }

    private static Color SyncStatusColor(SyncStatus syncStatus)
    {
        return syncStatus switch
        {
            SyncStatus.Synchronised => Color.Success,
            SyncStatus.FailedToSynchronise => Color.Error,
            SyncStatus.WaitingToSynchronise or SyncStatus.Synchronising => Color.Info,
            _ => Color.Warning
        };
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _dotNetReference?.Dispose();
        _cancellationTokenSource.Dispose();
    }
}
