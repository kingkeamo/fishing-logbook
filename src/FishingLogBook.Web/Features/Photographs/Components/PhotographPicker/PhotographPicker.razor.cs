using FishingLogBook.Web.Features.Photographs.Enums;
using FishingLogBook.Web.Features.Photographs.Models;
using FishingLogBook.Web.Features.Photographs.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Photographs.Components.PhotographPicker;

public partial class PhotographPicker : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _unsupportedFormat;
    private bool _unpreparablePhotograph;

    [Parameter, EditorRequired]
    public string IdPrefix { get; set; } = default!;

    [Parameter]
    public int MaxPhotographs { get; set; } = 10;

    [Parameter]
    public EventCallback<IReadOnlyList<PreparedPhotographModel>> PhotographsPrepared { get; set; }

    [Inject]
    private IPhotographPreparationService Preparation { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private string CameraElementId => $"{IdPrefix}-photo-camera";

    private string GalleryElementId => $"{IdPrefix}-photo-gallery";

    private string TakeElementId => $"{IdPrefix}-take-photo";

    private string ChooseElementId => $"{IdPrefix}-choose-photo";

    private string UnsupportedElementId => $"{IdPrefix}-photo-unsupported";

    private string UnpreparableElementId => $"{IdPrefix}-photo-unpreparable";

    private Task OnCameraSelectedAsync(InputFileChangeEventArgs args)
    {
        return PrepareAsync(args, PhotographSourceEnum.Camera);
    }

    private Task OnGallerySelectedAsync(InputFileChangeEventArgs args)
    {
        return PrepareAsync(args, PhotographSourceEnum.Gallery);
    }

    private async Task PrepareAsync(InputFileChangeEventArgs args, PhotographSourceEnum source)
    {
        var prepared = new List<PreparedPhotographModel>();
        var unsupported = false;
        var unpreparable = false;
        foreach (var file in args.GetMultipleFiles(MaxPhotographs))
        {
            var result = await Preparation.PrepareAsync(
                file,
                source,
                _cancellationTokenSource.Token);
            switch (result.Outcome)
            {
                case PhotographPreparationOutcomeEnum.Prepared:
                    prepared.Add(result.Photograph!);
                    break;
                case PhotographPreparationOutcomeEnum.UnsupportedContentType:
                    unsupported = true;
                    break;
                default:
                    unpreparable = true;
                    break;
            }
        }

        _unsupportedFormat = unsupported;
        _unpreparablePhotograph = unpreparable;
        if (prepared.Count > 0)
        {
            await PhotographsPrepared.InvokeAsync(prepared);
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
