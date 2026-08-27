using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Trips.Components.ActiveTripView;

public partial class ActiveTripView : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public TripModel Trip { get; set; } = default!;

    [Parameter]
    public string? StartedLabel { get; set; }

    [Parameter]
    public string? ElapsedLabel { get; set; }

    [Parameter]
    public string? GeneratedTitle { get; set; }

    [Parameter]
    public bool IsFinishing { get; set; }

    [Parameter]
    public EventCallback OnFinish { get; set; }

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private bool HasPlace
    {
        get
        {
            return !string.IsNullOrWhiteSpace(Trip.PlaceName);
        }
    }

    private string Heading
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Trip.Title))
            {
                return Trip.Title!;
            }

            return string.IsNullOrWhiteSpace(GeneratedTitle)
                ? Loc["Trip_ActiveLabel"].Value
                : GeneratedTitle!;
        }
    }
}
