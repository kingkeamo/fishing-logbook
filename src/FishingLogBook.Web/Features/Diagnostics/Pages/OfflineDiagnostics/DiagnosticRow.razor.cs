using Microsoft.AspNetCore.Components;

namespace FishingLogBook.Web.Features.Diagnostics.Pages.OfflineDiagnostics;

public partial class DiagnosticRow : ComponentBase
{
    [Parameter, EditorRequired] public string Label { get; set; } = string.Empty;
    [Parameter, EditorRequired] public string Value { get; set; } = string.Empty;
}
