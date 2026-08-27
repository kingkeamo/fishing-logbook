namespace FishingLogBook.Domain.Catches;

public sealed class Catch
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public Guid AnglerUserId { get; init; }

    public Guid RecordedByUserId { get; init; }

    public Guid? TripId { get; init; }

    public DateTimeOffset CaughtOn { get; init; }

    public string? SpeciesName { get; init; }

    public decimal? Weight { get; init; }

    public decimal? Length { get; init; }

    public string? Method { get; init; }

    public string? BaitOrLure { get; init; }

    public string? Notes { get; init; }

    public CatchLocation? Location { get; init; }

    public IReadOnlyList<CatchPhotograph> Photographs { get; init; } = [];
}
