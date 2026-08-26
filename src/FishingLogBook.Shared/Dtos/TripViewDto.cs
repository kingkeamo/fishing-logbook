namespace FishingLogBook.Shared.Dtos;

public sealed record TripViewDto(
    Guid Id,
    Guid OwnerUserId,
    string Status,
    DateTimeOffset StartedOn,
    DateTimeOffset? EndedOn = null,
    TripLocationDto? Location = null)
{
    public string? Title { get; init; }

    public string? PlaceName { get; init; }

    public DateTimeOffset CreatedOn { get; init; }

    public DateTimeOffset UpdatedOn { get; init; }
}
