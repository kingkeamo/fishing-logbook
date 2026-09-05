using FishingLogBook.Web.Features.Photographs.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Photographs.Components.PhotographGridSelector;

public partial class PhotographGridSelector : ComponentBase
{
    private readonly HashSet<Guid> _selectedIds = [];

    [Parameter, EditorRequired] public IReadOnlyList<PhotographCarouselItemModel> Photographs { get; set; } = [];
    [Parameter, EditorRequired] public string IdPrefix { get; set; } = string.Empty;
    [Parameter] public EventCallback<IReadOnlySet<Guid>> SelectedIdsChanged { get; set; }
    [Parameter] public EventCallback<Guid?> ActivePhotographIdChanged { get; set; }
    [Parameter] public RenderFragment<int>? SelectedActions { get; set; }

    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override void OnParametersSet()
    {
        _selectedIds.RemoveWhere(selectedId => Photographs.All(photo => photo.Id != selectedId));
    }

    private async Task SelectAsync(Guid photographId, bool selected)
    {
        if (selected)
        {
            _selectedIds.Add(photographId);
        }
        else
        {
            _selectedIds.Remove(photographId);
        }

        await SelectedIdsChanged.InvokeAsync(_selectedIds);
    }

    private static string? ToPhotoUrl(PhotographCarouselItemModel photograph)
    {
        return photograph.Bytes is { Length: > 0 }
            ? $"data:{photograph.ContentType};base64,{Convert.ToBase64String(photograph.Bytes)}"
            : photograph.RemoteUrl;
    }

    private Task OpenAsync(Guid photographId)
    {
        return ActivePhotographIdChanged.InvokeAsync(photographId);
    }
}
