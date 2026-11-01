using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Shared.Dtos;

public sealed record PlannedTripOptionDto(
    Guid TripId,
    DateTimeOffset PlannedStartOn,
    Guid OrganiserUserId,
    string? OrganiserName)
{
    public string? Title { get; init; }

    public string? PlaceName { get; init; }

    public string Role { get; init; } = TripParticipantConstants.Owner;

    public IReadOnlyList<PlannedTripParticipantDto> Participants { get; init; } = [];
}
