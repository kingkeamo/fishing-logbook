using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Features.Catch.Pages.RecordCatch;

public partial class RecordCatch : ComponentBase, IDisposable
{
    private const long MaxPhotographBytes = 10 * 1024 * 1024;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<PendingPhotograph> _photographs = [];
    private DateTimeOffset? _caughtOn;
    private bool _isSaving;
    private bool _saveFailed;

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private bool CanSave => _photographs.Count > 0 && !_isSaving;

    private string CaughtOnDisplay => _caughtOn?.ToString("g") ?? string.Empty;

    private async Task OnPhotographSelected(InputFileChangeEventArgs args)
    {
        foreach (var file in args.GetMultipleFiles(10))
        {
            await AddPhotographAsync(file);
        }
    }

    private async Task AddPhotographAsync(IBrowserFile file)
    {
        await using var stream = file.OpenReadStream(MaxPhotographBytes);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, _cancellationTokenSource.Token);
        var bytes = buffer.ToArray();
        var contentType = PhotographContentTypeConstants.IsAllowed(file.ContentType)
            ? file.ContentType
            : PhotographContentTypeConstants.Jpeg;
        _caughtOn ??= DateTimeOffset.Now;
        _photographs.Add(new PendingPhotograph(
            Guid.NewGuid(),
            contentType,
            bytes,
            $"data:{contentType};base64,{Convert.ToBase64String(bytes)}"));
        _saveFailed = false;
    }

    private void RemovePhotograph(Guid photographId)
    {
        _photographs.RemoveAll(photograph => photograph.Id == photographId);
        if (_photographs.Count == 0)
        {
            _caughtOn = null;
        }
    }

    private async Task SaveAsync()
    {
        if (!CanSave)
        {
            return;
        }

        _isSaving = true;
        _saveFailed = false;
        await InvokeAsync(StateHasChanged);
        try
        {
            var catchId = Guid.NewGuid();
            var photographs = _photographs
                .Select(photograph => new CatchPhotographModel(
                    photograph.Id,
                    catchId,
                    photograph.ContentType,
                    photograph.Bytes))
                .ToArray();
            await CatchStore.SaveAsync(
                new CatchModel(catchId, _caughtOn ?? DateTimeOffset.Now, photographs),
                _cancellationTokenSource.Token);
            Snackbar.Add(Loc["Catch_Saved"], Severity.Success);
            _photographs.Clear();
            _caughtOn = null;
        }
        catch (Exception)
        {
            _saveFailed = true;
        }
        finally
        {
            _isSaving = false;
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }

    private sealed record PendingPhotograph(
        Guid Id,
        string ContentType,
        byte[] Bytes,
        string PreviewUrl);
}
