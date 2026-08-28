namespace FishingLogBook.Shared.Dtos;

public sealed record TripCatchAssociationDto(
    IReadOnlyList<Guid> AssociatedCatchIds,
    IReadOnlyList<Guid> RejectedCatchIds);
