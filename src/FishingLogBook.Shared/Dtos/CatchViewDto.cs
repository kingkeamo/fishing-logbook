namespace FishingLogBook.Shared.Dtos;

public sealed record CatchViewDto(
    Guid Id,
    Guid UserId,
    DateTimeOffset CaughtOn,
    CatchLocationExposureDto? Location = null)
{
    public Guid AnglerUserId { get; init; }

    public Guid RecordedByUserId { get; init; }

    public string? SpeciesName { get; init; }

    public decimal? Weight { get; init; }

    public decimal? Length { get; init; }

    public string? Method { get; init; }

    public string? BaitOrLure { get; init; }

    public string? Notes { get; init; }

    public IReadOnlyList<CatchPhotographViewDto> Photographs { get; init; } = [];
}
