namespace FishingLogBook.Shared.Dtos;

public sealed record PlannedTripParticipantDto(
    Guid UserId,
    string? DisplayName,
    string Status);
