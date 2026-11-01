namespace FishingLogBook.Mobile.Features.Trips.Models;

public sealed record PlannedTripChoiceModel(PlannedTripOptionModel? Selected, bool IsDismissed)
{
    public static PlannedTripChoiceModel StartNew { get; } = new(Selected: null, IsDismissed: false);

    public static PlannedTripChoiceModel Dismissed { get; } = new(Selected: null, IsDismissed: true);
}
