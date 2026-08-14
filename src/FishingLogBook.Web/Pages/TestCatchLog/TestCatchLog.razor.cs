using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Models;
using FishingLogBook.Web.Offline;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using MudBlazor;

namespace FishingLogBook.Web.Pages.TestCatchLog;

public partial class TestCatchLog : ComponentBase, IDisposable
{
    private const long MaxPhotographBytes = 10 * 1024 * 1024;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Dictionary<Guid, string> _photographUrls = [];
    private DotNetObjectReference<TestCatchLog>? _dotNetReference;

    private string _speciesName = string.Empty;
    private string _notes = string.Empty;
    private IReadOnlyList<TestCatch> _catches = [];
    private bool _isSaving;
    private byte[]? _pendingPhotographBytes;
    private string? _pendingPhotographContentType;
    private string? _pendingPhotographUrl;

    [Inject]
    private ITestCatchStore TestCatchStore { get; set; } = default!;

    [Inject]
    private ITestCatchPhotoStore TestCatchPhotoStore { get; set; } = default!;

    [Inject]
    private ITestCatchSynchroniser TestCatchSynchroniser { get; set; } = default!;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private bool CanSave => !_isSaving && !string.IsNullOrWhiteSpace(_speciesName);

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        await TestCatchSynchroniser.SynchronisePendingAsync(_cancellationTokenSource.Token);
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

        TestCatchPhotograph? photograph = null;
        if (_pendingPhotographBytes is { Length: > 0 } && _pendingPhotographContentType is not null)
        {
            photograph = new TestCatchPhotograph(
                Guid.NewGuid(),
                _pendingPhotographContentType,
                SyncStatus.SavedLocally);
        }

        var testCatch = new TestCatch(
            Guid.NewGuid(),
            _speciesName.Trim(),
            DateTimeOffset.UtcNow,
            string.IsNullOrWhiteSpace(_notes) ? null : _notes.Trim(),
            SyncStatus.SavedLocally,
            photograph);

        await TestCatchStore.SaveAsync(testCatch, _cancellationTokenSource.Token);
        if (photograph is not null && _pendingPhotographBytes is not null)
        {
            await TestCatchPhotoStore.PutAsync(
                testCatch.Id,
                _pendingPhotographBytes,
                photograph.ContentType,
                _cancellationTokenSource.Token);
        }

        _speciesName = string.Empty;
        _notes = string.Empty;
        _pendingPhotographBytes = null;
        _pendingPhotographContentType = null;
        _pendingPhotographUrl = null;
        await TestCatchSynchroniser.SynchronisePendingAsync(_cancellationTokenSource.Token);
        await LoadAsync();
        _isSaving = false;
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

    private async Task LoadAsync()
    {
        _catches = await TestCatchStore.GetAllAsync(_cancellationTokenSource.Token);
        _photographUrls.Clear();
        foreach (var testCatch in _catches)
        {
            var local = await TestCatchPhotoStore.GetAsync(testCatch.Id, _cancellationTokenSource.Token);
            if (local is not null)
            {
                _photographUrls[testCatch.Id] = $"data:{local.ContentType};base64,{Convert.ToBase64String(local.Bytes)}";
            }
            else if (!string.IsNullOrWhiteSpace(testCatch.Photograph?.RemoteUrl))
            {
                _photographUrls[testCatch.Id] = testCatch.Photograph.RemoteUrl;
            }
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
