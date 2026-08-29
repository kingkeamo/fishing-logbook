namespace FishingLogBook.Shared.Dtos;

public sealed record TripContributorDto(
    Guid UserId,
    string? DisplayName,
    string? PhotographUrl)
{
    public bool IsOwner { get; init; }
}
