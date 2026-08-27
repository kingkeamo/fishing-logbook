namespace FishingLogBook.Domain.Trips;

public sealed class TripCatchSummary
{
    public Guid Id { get; init; }

    public DateTimeOffset CaughtOn { get; init; }

    public string? SpeciesName { get; init; }
}
