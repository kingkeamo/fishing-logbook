namespace FishingLogBook.Shared.Dtos;

public sealed record TripPhotographViewDto(
    Guid Id,
    string ContentType,
    DateTimeOffset AddedOn,
    string? Url = null,
    DateTimeOffset? CapturedOn = null)
{
    public Guid ContributedByUserId { get; init; }
}
