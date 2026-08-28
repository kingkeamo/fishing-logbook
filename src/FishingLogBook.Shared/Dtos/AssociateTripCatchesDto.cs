namespace FishingLogBook.Shared.Dtos;

public sealed record AssociateTripCatchesDto(IReadOnlyList<Guid> CatchIds);
