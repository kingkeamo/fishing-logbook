using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Models;
using FishingLogBook.Web.Offline;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using MudBlazor;

namespace FishingLogBook.Web.Pages.TestCatchLog;

public partial class TestCatchLog : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private DotNetObjectReference<TestCatchLog>? _dotNetReference;

    private string _speciesName = string.Empty;
    private string _notes = string.Empty;
    private IReadOnlyList<TestCatch> _catches = [];
    private bool _isSaving;

    [Inject]
    private ITestCatchStore TestCatchStore { get; set; } = default!;

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

    private async Task SaveAsync()
    {
        if (!CanSave)
        {
            return;
        }

        _isSaving = true;

        var testCatch = new TestCatch(
            Guid.NewGuid(),
            _speciesName.Trim(),
            DateTimeOffset.UtcNow,
            string.IsNullOrWhiteSpace(_notes) ? null : _notes.Trim(),
            SyncStatus.SavedLocally);

        await TestCatchStore.SaveAsync(testCatch, _cancellationTokenSource.Token);
        _speciesName = string.Empty;
        _notes = string.Empty;
        await TestCatchSynchroniser.SynchronisePendingAsync(_cancellationTokenSource.Token);
        await LoadAsync();
        _isSaving = false;
    }

    private async Task RetryAsync(Guid id)
    {
        await TestCatchSynchroniser.RetryAsync(id, _cancellationTokenSource.Token);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _catches = await TestCatchStore.GetAllAsync(_cancellationTokenSource.Token);
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
