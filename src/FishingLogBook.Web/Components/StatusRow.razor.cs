using FishingLogBook.Web.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FishingLogBook.Web.Components;

public partial class StatusRow : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    public StatusState State { get; set; } = StatusState.Checking;

    [Parameter]
    public string? DetailText { get; set; }

    private string RowId
    {
        get
        {
            return $"status-row-{Label.ToLowerInvariant()}";
        }
    }

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
                StatusState.Online => "Online",
                StatusState.Degraded => "Degraded",
                StatusState.Offline => "Offline",
                _ => "Checking..."
            };
        }
    }
}
