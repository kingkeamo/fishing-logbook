namespace FishingLogBook.Shared.Dtos;

public sealed record TripDto(
    Guid Id,
    string Status,
    DateTimeOffset StartedOn,
    DateTimeOffset? EndedOn = null,
    TripLocationDto? Location = null)
{
    public Guid OwnerUserId { get; init; }

    public string? Title { get; init; }

    public string? PlaceName { get; init; }
}
