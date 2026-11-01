namespace FishingLogBook.Mobile.Features.Trips.Models;

public sealed record TripCatchChangeModel(IReadOnlyList<Guid> ChangedCatchIds, IReadOnlyList<Guid> RejectedCatchIds);
