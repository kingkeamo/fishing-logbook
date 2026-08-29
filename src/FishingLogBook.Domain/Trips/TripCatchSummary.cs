namespace FishingLogBook.Domain.Trips;

public sealed class TripCatchSummary
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public Guid AnglerUserId { get; init; }

    public Guid RecordedByUserId { get; init; }

    public DateTimeOffset CaughtOn { get; init; }

    public string? SpeciesName { get; init; }

    public decimal? Weight { get; init; }

    public decimal? Length { get; init; }

    public Guid? PhotographId { get; init; }
}
