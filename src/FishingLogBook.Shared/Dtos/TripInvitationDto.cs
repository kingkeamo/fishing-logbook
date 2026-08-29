namespace FishingLogBook.Shared.Dtos;

public sealed record TripInvitationDto(
    Guid TripId,
    Guid OwnerUserId,
    string? OwnerDisplayName,
    string? Title,
    string? PlaceName,
    DateTimeOffset StartedOn,
    DateTimeOffset InvitedOn);
