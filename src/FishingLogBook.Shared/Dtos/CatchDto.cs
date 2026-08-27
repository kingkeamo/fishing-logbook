namespace FishingLogBook.Shared.Dtos;

public sealed record CatchDto(
    Guid Id,
    DateTimeOffset CaughtOn,
    IReadOnlyList<CatchPhotographDto> Photographs,
    CatchLocationDto? Location = null)
{
    public Guid UserId { get; init; }

    public Guid AnglerUserId { get; init; }

    public Guid RecordedByUserId { get; init; }

    public Guid? TripId { get; init; }

    public string? SpeciesName { get; init; }

    public decimal? Weight { get; init; }

    public decimal? Length { get; init; }

    public string? Method { get; init; }

    public string? BaitOrLure { get; init; }

    public string? Notes { get; init; }
}
