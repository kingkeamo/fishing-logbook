namespace FishingLogBook.Shared.Dtos;

public sealed record CatchDto(
    Guid Id,
    DateTimeOffset CaughtOn,
    IReadOnlyList<CatchPhotographDto> Photographs)
{
    public Guid UserId { get; init; }
}
