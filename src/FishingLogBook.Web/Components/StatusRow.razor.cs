using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Components;

public partial class StatusRow : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public string Id { get; set; } = string.Empty;

    [Parameter]
    [EditorRequired]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    public StatusState State { get; set; } = StatusState.Checking;

    [Parameter]
    public string? DetailText { get; set; }

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private string RowId => $"status-row-{Id}";

    private Color StatusColor
    {
        get
        {
            return State switch
            {
                StatusState.Online => Color.Success,
                StatusState.Degraded => Color.Warning,
                StatusState.Offline => Color.Error,
                _ => Color.Default
            };
        }
    }

    private string StatusText
    {
        get
        {
            return State switch
            {
                StatusState.Online => Loc["Status_Online"],
                StatusState.Degraded => Loc["Status_Degraded"],
                StatusState.Offline => Loc["Status_Offline"],
                _ => Loc["Status_Checking"]
            };
        }
    }
}
