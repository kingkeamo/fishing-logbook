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
}
