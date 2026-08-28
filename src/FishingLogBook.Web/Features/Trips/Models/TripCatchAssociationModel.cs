namespace FishingLogBook.Web.Features.Trips.Models;

public sealed record TripCatchAssociationModel(
    IReadOnlyList<Guid> AssociatedCatchIds,
    IReadOnlyList<Guid> RejectedCatchIds);
