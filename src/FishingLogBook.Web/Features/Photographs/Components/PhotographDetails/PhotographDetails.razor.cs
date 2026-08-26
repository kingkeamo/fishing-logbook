using System.Globalization;
using FishingLogBook.Web.Features.Photographs.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Photographs.Components.PhotographDetails;

public partial class PhotographDetails : ComponentBase
{
    [Parameter, EditorRequired]
    public string IdPrefix { get; set; } = default!;

    [Parameter]
    public Guid? PhotographId { get; set; }

    [Parameter]
    public PhotographMetadataModel? Metadata { get; set; }

    [Parameter]
    public string? CapturedOnLocal { get; set; }

    [Parameter]
    public bool ShowUseDetails { get; set; }

    [Parameter]
    public bool IsChosen { get; set; }

    [Parameter]
    public EventCallback OnUseDetails { get; set; }

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private string ContainerElementId => $"{IdPrefix}-photo-current-metadata";

    private string CapturedOnElementId => $"{IdPrefix}-photo-current-date";

    private string LocationElementId => $"{IdPrefix}-photo-current-location";

    private string UseDetailsElementId => $"{IdPrefix}-photo-use-details";

    private string CapturedOnValue => CapturedOnLocal ?? string.Empty;

    private string CapturedOnLabel =>
        string.IsNullOrEmpty(CapturedOnValue)
            ? Loc["Photograph_CapturedOnUnknown"]
            : FormatLocalDateTime(CapturedOnValue);

    private string LocationLabel =>
        Metadata?.HasCoordinates == true
            ? Loc["Photograph_LocationAvailable"]
            : Loc["Photograph_LocationUnavailable"];

    private bool CanUseDetails =>
        Metadata is not null && (Metadata.CapturedOn.HasValue || Metadata.HasCoordinates);

    private Task OnUseDetailsAsync()
    {
        return CanUseDetails ? OnUseDetails.InvokeAsync() : Task.CompletedTask;
    }

    private static string FormatLocalDateTime(string localValue)
    {
        return DateTime.TryParseExact(
            localValue,
            ["yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed.ToString("g", CultureInfo.CurrentCulture)
            : localValue;
    }
}
