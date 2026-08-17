namespace FishingLogBook.Shared.Dtos;

public sealed record CatchDto(
    Guid Id,
    DateTimeOffset CaughtOn,
    IReadOnlyList<CatchPhotographDto> Photographs,
    CatchLocationDto? Location = null)
{
    public Guid UserId { get; init; }
}
