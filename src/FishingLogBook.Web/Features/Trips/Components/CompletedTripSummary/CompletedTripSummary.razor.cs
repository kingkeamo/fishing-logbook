using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Trips.Components.CompletedTripSummary;

public partial class CompletedTripSummary : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public TripModel Trip { get; set; } = default!;

    [Parameter]
    public string? DurationLabel { get; set; }

    [Parameter]
    public string? GeneratedTitle { get; set; }

    [Parameter]
    public string LogbookHref { get; set; } = "/catches";

    [Parameter]
    public string CatchBaseHref { get; set; } = "/catches";

    [Parameter]
    public bool ShowNotes { get; set; } = true;

    [Parameter]
    public IReadOnlyList<TripTimelineItemModel> Timeline { get; set; } = [];

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

            return GeneratedTitle ?? string.Empty;
        }
    }
}
