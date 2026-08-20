using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Components.CatchPhotographCarousel;

public partial class CatchPhotographCarousel : ComponentBase
{
    private IReadOnlyList<Guid> _photographIds = [];
    private IReadOnlyList<string> _photoUrls = [];
    private int _currentPhotographIndex;

    [Parameter, EditorRequired]
    public IReadOnlyList<CatchPhotographCarouselItemModel> Photographs { get; set; } = [];

    [Parameter]
    public bool Editable { get; set; }

    [Parameter]
    public bool Compact { get; set; }

    [Parameter, EditorRequired]
    public string IdPrefix { get; set; } = default!;

    [Parameter]
    public string? IdSuffix { get; set; }

    [Parameter]
    public EventCallback<Guid> OnRemovePhotograph { get; set; }

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override void OnParametersSet()
    {
        var photographs = Photographs
            .Where(item =>
                item.Bytes is { Length: > 0 } ||
                !string.IsNullOrWhiteSpace(item.RemoteUrl))
            .ToArray();

        var currentIds = photographs
            .Select(item => item.Id)
            .ToArray();

        if (currentIds.SequenceEqual(_photographIds))
        {
            return;
        }

        _photographIds = currentIds;
        _photoUrls = photographs
            .Select(ToPhotoUrl)
            .ToArray();

        if (_currentPhotographIndex >= _photoUrls.Count)
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

    private void PreviousPhotograph()
    {
        if (PhotographCount <= 1)
        {
            return;
        }

        _currentPhotographIndex =
            (_currentPhotographIndex - 1 + PhotographCount) % PhotographCount;
    }

    private void NextPhotograph()
    {
        if (PhotographCount <= 1)
        {
            return;
        }

        _currentPhotographIndex =
            (_currentPhotographIndex + 1) % PhotographCount;
    }

    private Task RemoveCurrentPhotographAsync()
    {
        return CurrentPhotographId is { } id
            ? OnRemovePhotograph.InvokeAsync(id)
            : Task.CompletedTask;
    }
}
