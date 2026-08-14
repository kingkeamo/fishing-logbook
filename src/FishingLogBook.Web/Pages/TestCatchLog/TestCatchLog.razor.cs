using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Models;
using FishingLogBook.Web.Offline;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Pages.TestCatchLog;

public partial class TestCatchLog : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private string _speciesName = string.Empty;
    private string _notes = string.Empty;
    private IReadOnlyList<TestCatch> _catches = [];
    private bool _isSaving;

    [Inject]
    private ITestCatchStore TestCatchStore { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private bool CanSave => !_isSaving && !string.IsNullOrWhiteSpace(_speciesName);

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
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
        await LoadAsync();
        _isSaving = false;
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

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
