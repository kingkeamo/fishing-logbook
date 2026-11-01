namespace FishingLogBook.Web.Features.Trips.Models;

public sealed record PlannedTripChoice(PlannedTripOptionModel? Selected, bool IsDismissed)
{
    public static PlannedTripChoice StartNew { get; } = new(Selected: null, IsDismissed: false);

    public static PlannedTripChoice Dismissed { get; } = new(Selected: null, IsDismissed: true);
}
