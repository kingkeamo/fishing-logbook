using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Components.CatchPhotographCarousel;

public partial class CatchPhotographCarousel : ComponentBase
{
    private const double SwipeThresholdPixels = 40;

    private IReadOnlyList<Guid> _photographIds = [];
    private IReadOnlyList<string> _photoUrls = [];
    private int _currentPhotographIndex;
    private double _pointerStartX;

    [Parameter, EditorRequired]
    public IReadOnlyList<CatchPhotographCarouselItemModel> Photographs { get; set; } = [];

    [Parameter]
    public bool Editable { get; set; }

    [Parameter]
    public bool Compact { get; set; }

    [Parameter]
    public bool ShowSinglePhotographCount { get; set; }

    [Parameter, EditorRequired]
    public string IdPrefix { get; set; } = default!;

    [Parameter]
    public string? IdSuffix { get; set; }

    [Parameter]
    public EventCallback<Guid> OnRemovePhotograph { get; set; }

    [Parameter]
    public Guid? ActivePhotographId { get; set; }

    [Parameter]
    public EventCallback<Guid?> ActivePhotographIdChanged { get; set; }

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override void OnParametersSet()
    {
        var photographs = Photographs
            .Where(item =>
                item.Bytes is { Length: > 0 } ||
                !string.IsNullOrWhiteSpace(item.RemoteUrl))
            .ToArray();

        var currentPhotographId = CurrentPhotographId;
        var currentIds = photographs
            .Select(item => item.Id)
            .ToArray();

        _photographIds = currentIds;
        _photoUrls = photographs
            .Select(ToPhotoUrl)
            .ToArray();

        var requestedPhotographId = ActivePhotographId ?? currentPhotographId;
        if (requestedPhotographId is { } photographId)
        {
            var retainedIndex = Array.IndexOf(currentIds, photographId);
            _currentPhotographIndex = retainedIndex >= 0 ? retainedIndex : 0;
        }
        else if (_currentPhotographIndex >= _photoUrls.Count)
        {
            _currentPhotographIndex = 0;
        }
    }

    private static string ToPhotoUrl(CatchPhotographCarouselItemModel photograph)
    {
        if (photograph.Bytes is { Length: > 0 })
        {
            return $"data:{photograph.ContentType};base64,{Convert.ToBase64String(photograph.Bytes)}";
        }

        return photograph.RemoteUrl!;
    }

    private string ContainerClass =>
        Compact
            ? "catch-photograph-carousel catch-photograph-carousel-compact"
            : "catch-photograph-carousel catch-photograph-carousel-large";

    private int PhotographCount => _photoUrls.Count;

    private bool HasMultiplePhotographs => PhotographCount > 1;

    private bool ShowNavigationRow => HasMultiplePhotographs || (Editable && PhotographCount > 0);

    private int CurrentPhotographNumber =>
        PhotographCount == 0
            ? 0
            : _currentPhotographIndex + 1;

    private string? CurrentPhotoUrl =>
        PhotographCount == 0
            ? null
            : _photoUrls[_currentPhotographIndex];

    private Guid? CurrentPhotographId =>
        PhotographCount == 0
            ? null
            : _photographIds[_currentPhotographIndex];

    private string PhotoElementId =>
        HasMultiplePhotographs
            ? $"{Combine("photo")}-{_currentPhotographIndex}"
            : Combine("photo");

    private string NoPhotoElementId => Combine("no-photo");

    private string NavigationElementId => Combine("photo-navigation");

    private string PreviousElementId => Combine("photo-previous");

    private string CountElementId => Combine("photo-count");

    private string NextElementId => Combine("photo-next");

    private string RemoveElementId => Combine("photo-remove");

    private string Combine(string role)
    {
        return string.IsNullOrEmpty(IdSuffix)
            ? $"{IdPrefix}-{role}"
            : $"{IdPrefix}-{role}-{IdSuffix}";
    }

    private string CurrentPhotographAlt =>
        HasMultiplePhotographs
            ? Loc[
                "Catch_PhotographAltNumbered",
                CurrentPhotographNumber,
                PhotographCount]
            : Loc["Catch_PhotographAlt"];

    private Task PreviousPhotographAsync()
    {
        if (PhotographCount <= 1)
        {
            return Task.CompletedTask;
        }

        return SetCurrentIndexAsync(
            (_currentPhotographIndex - 1 + PhotographCount) % PhotographCount);
    }

    private Task NextPhotographAsync()
    {
        if (PhotographCount <= 1)
        {
            return Task.CompletedTask;
        }

        return SetCurrentIndexAsync(
            (_currentPhotographIndex + 1) % PhotographCount);
    }

    private async Task SetCurrentIndexAsync(int index)
    {
        _currentPhotographIndex = index;
        await ActivePhotographIdChanged.InvokeAsync(CurrentPhotographId);
    }

    private Task OnKeyDownAsync(KeyboardEventArgs args)
    {
        return args.Key switch
        {
            "ArrowLeft" => PreviousPhotographAsync(),
            "ArrowRight" => NextPhotographAsync(),
            _ => Task.CompletedTask
        };
    }

    private void OnPointerDown(PointerEventArgs args)
    {
        _pointerStartX = args.ClientX;
    }

    private Task OnPointerUpAsync(PointerEventArgs args)
    {
        var delta = args.ClientX - _pointerStartX;
        if (delta <= -SwipeThresholdPixels)
        {
            return NextPhotographAsync();
        }

        return delta >= SwipeThresholdPixels
            ? PreviousPhotographAsync()
            : Task.CompletedTask;
    }

    private Task RemoveCurrentPhotographAsync()
    {
        return CurrentPhotographId is { } id
            ? OnRemovePhotograph.InvokeAsync(id)
            : Task.CompletedTask;
    }
}
