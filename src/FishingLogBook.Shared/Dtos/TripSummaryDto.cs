using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Shared.Dtos;

public sealed record TripSummaryDto(
    Guid Id,
    string Status,
    DateTimeOffset StartedOn,
    DateTimeOffset? EndedOn = null)
{
    public string? Title { get; init; }

    public string? PlaceName { get; init; }

    public int CatchCount { get; init; }

    public int PhotographCount { get; init; }

    public int NoteCount { get; init; }

    public Guid OwnerUserId { get; init; }

    public string Role { get; init; } = TripParticipantConstants.Owner;

    public int ParticipantCount { get; init; }

    public bool IsShared
    {
        get
        {
            return ParticipantCount > 0;
        }
    }
}
