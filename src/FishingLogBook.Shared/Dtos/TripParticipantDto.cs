namespace FishingLogBook.Shared.Dtos;

public sealed record TripParticipantDto(
    Guid UserId,
    string Status,
    string? DisplayName,
    string? PhotographUrl,
    DateTimeOffset InvitedOn)
{
    public bool IsOwner { get; init; }

    public DateTimeOffset? RespondedOn { get; init; }
}
