using FishingLogBook.Domain.Enums;

namespace FishingLogBook.Domain.Trips;

public sealed class TripSummary
{
    public Guid Id { get; init; }

    public TripStatusEnum Status { get; init; }

    public DateTimeOffset StartedOn { get; init; }

    public DateTimeOffset? EndedOn { get; init; }

    public string? Title { get; init; }

    public string? PlaceName { get; init; }

    public int CatchCount { get; init; }

    public int PhotographCount { get; init; }

    public int NoteCount { get; init; }
}
