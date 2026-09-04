using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Import.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FishingLogBook.Web.Features.Import.Components.ImportPhotographPicker;

public partial class ImportPhotographPicker : ComponentBase, IAsyncDisposable
{
    private CancellationTokenSource? _selectionCancellation;
    private Task _selectionTask = Task.CompletedTask;

    [Parameter, EditorRequired]
    public string Id { get; set; } = default!;

    [Parameter]
    public EventCallback<IReadOnlyList<ImportSelectedPhotoModel>> PhotosPrepared { get; set; }

    [Parameter]
    public EventCallback SelectionLimitExceeded { get; set; }

    [Parameter]
    public EventCallback SelectionStarted { get; set; }

    [Inject]
    private IImportPhotoPreparationService Preparation { get; set; } = default!;

    private async Task OnSelectedAsync(InputFileChangeEventArgs args)
    {
        await CancelSelectionAsync();
        var files = args.GetMultipleFiles(ImportPhotoPreparationService.MaxPhotographs + 1);
        if (files.Count > ImportPhotoPreparationService.MaxPhotographs)
        {
            await SelectionLimitExceeded.InvokeAsync();
            return;
        }

        await SelectionStarted.InvokeAsync();
        _selectionCancellation = new CancellationTokenSource();
        _selectionTask = PrepareAsync(files, _selectionCancellation.Token);
        await _selectionTask;
    }

    private async Task PrepareAsync(
        IReadOnlyList<IBrowserFile> files,
        CancellationToken cancellationToken)
    {
        try
        {
            var photos = await Preparation.PrepareSelectionAsync(files, cancellationToken);
            await PhotosPrepared.InvokeAsync(photos);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task CancelSelectionAsync()
    {
        if (_selectionCancellation is null)
        {
            return;
        }

        await _selectionCancellation.CancelAsync();
        await _selectionTask;
        _selectionCancellation.Dispose();
        _selectionCancellation = null;
    }

    public async ValueTask DisposeAsync()
    {
        await CancelSelectionAsync();
    }
}
