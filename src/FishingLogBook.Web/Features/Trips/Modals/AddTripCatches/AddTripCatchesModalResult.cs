namespace FishingLogBook.Web.Features.Trips.Modals.AddTripCatches;

public sealed record AddTripCatchesModalResult(
    IReadOnlyList<Guid> AssociatedCatchIds,
    IReadOnlyList<Guid> RejectedCatchIds);
