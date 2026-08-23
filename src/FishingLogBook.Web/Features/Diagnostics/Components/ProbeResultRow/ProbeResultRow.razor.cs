using Microsoft.AspNetCore.Components;

namespace FishingLogBook.Web.Features.Diagnostics.Components.ProbeResultRow;

public partial class ProbeResultRow : ComponentBase
{
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public string Value { get; set; } = string.Empty;
}
