using System.Globalization;
using FishingLogBook.Shared.Dtos;
using Microsoft.AspNetCore.Components;

namespace FishingLogBook.Web.Features.Trips.Components.TripSelector;

public partial class TripSelector : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<TripSummaryDto> Trips { get; set; } = [];

    [Parameter]
    public Guid? SelectedTripId { get; set; }

    [Parameter]
    public EventCallback<Guid> SelectedTripIdChanged { get; set; }

    [Parameter]
    public string? Label { get; set; }

    private async Task SelectAsync(Guid tripId)
    {
        if (SelectedTripIdChanged.HasDelegate)
        {
            await SelectedTripIdChanged.InvokeAsync(tripId);
        }
    }

    private static string DisplayLabel(TripSummaryDto trip)
    {
        return string.IsNullOrWhiteSpace(trip.Title)
            ? trip.StartedOn.ToString("g", CultureInfo.CurrentCulture)
            : trip.Title;
    }
}
