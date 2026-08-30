namespace FishingLogBook.Shared.Dtos;

public sealed record TripPhotographDto(
    Guid Id,
    Guid TripId,
    string ObjectKey,
    string ContentType,
    DateTimeOffset AddedOn,
    DateTimeOffset? CapturedOn = null)
{
    public Guid ContributedByUserId { get; init; }
}
