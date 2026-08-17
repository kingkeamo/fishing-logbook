using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Pages.RecordCatch;

public partial class RecordCatch : ComponentBase, IDisposable
{
    private const long MaxPhotographBytes = 10 * 1024 * 1024;
    private const double SwipeThresholdPixels = 40;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<PendingPhotograph> _photographs = [];
    private DateTimeOffset? _caughtOn;
    private int _carouselIndex;
    private double _pointerStartX;
    private bool _isSaving;
    private bool _isSaved;
    private bool _saveFailed;

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private bool CanSave
    {
        get
        {
            return !_isSaved && _photographs.Count > 0 && !_isSaving;
        }
    }

    private bool CanShowPrevious
    {
        get
        {
            return _carouselIndex > 0;
        }
    }

    private bool CanShowNext
    {
        get
        {
            return _carouselIndex < _photographs.Count - 1;
        }
    }

    private string CaughtOnDisplay
    {
        get
        {
            return _caughtOn?.ToString("g") ?? string.Empty;
        }
    }

    private string PhotoPosition
    {
        get
        {
            return Loc["Catch_PhotoPosition", _carouselIndex + 1, _photographs.Count];
        }
    }

    private PendingPhotograph? CurrentPhotograph
    {
        get
        {
            if (_photographs.Count == 0
                || _carouselIndex < 0
                || _carouselIndex >= _photographs.Count)
            {
                return null;
            }

            return _photographs[_carouselIndex];
        }
    }

    private async Task OnPhotographSelected(InputFileChangeEventArgs args)
    {
        if (_isSaved)
        {
            return;
        }

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
        _carouselIndex = _photographs.Count - 1;
        _saveFailed = false;
    }

    private void RemoveCurrentPhotograph()
    {
        var current = CurrentPhotograph;
        if (_isSaved || current is null)
        {
            return;
        }

        var removedIndex = _photographs.FindIndex(photograph => photograph.Id == current.Id);
        _photographs.RemoveAll(photograph => photograph.Id == current.Id);
        if (_photographs.Count == 0)
        {
            _carouselIndex = 0;
            _caughtOn = null;
            return;
        }

        _carouselIndex = Math.Min(removedIndex, _photographs.Count - 1);
    }

    private void ShowPrevious()
    {
        if (!CanShowPrevious)
        {
            return;
        }

        _carouselIndex -= 1;
    }

    private void ShowNext()
    {
        if (!CanShowNext)
        {
            return;
        }

        _carouselIndex += 1;
    }

    private void OnPointerDown(PointerEventArgs args)
    {
        _pointerStartX = args.ClientX;
    }

    private void OnPointerUp(PointerEventArgs args)
    {
        var delta = args.ClientX - _pointerStartX;
        if (delta <= -SwipeThresholdPixels)
        {
            ShowNext();
            return;
        }

        if (delta >= SwipeThresholdPixels)
        {
            ShowPrevious();
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
            _isSaved = true;
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

    private void RecordAnotherCatch()
    {
        _photographs.Clear();
        _caughtOn = null;
        _carouselIndex = 0;
        _isSaved = false;
        _saveFailed = false;
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
