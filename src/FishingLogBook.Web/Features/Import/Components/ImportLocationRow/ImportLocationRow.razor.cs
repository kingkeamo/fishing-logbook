using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Import.Components.ImportLocationRow;

public partial class ImportLocationRow : ComponentBase
{
    [Parameter, EditorRequired]
    public string IdPrefix { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    public bool CanEdit { get; set; }

    [Parameter]
    public bool CanRemove { get; set; }

    [Parameter]
    public bool CanSelect { get; set; }

    [Parameter]
    public bool IsSelected { get; set; }

    [Parameter]
    public EventCallback Edit { get; set; }

    [Parameter]
    public EventCallback Remove { get; set; }

    [Parameter]
    public EventCallback Select { get; set; }

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;
}
