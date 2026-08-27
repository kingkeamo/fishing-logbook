namespace FishingLogBook.Shared.Dtos;

public sealed record TripCatchSummaryDto(Guid Id, DateTimeOffset CaughtOn)
{
    public string? SpeciesName { get; init; }
}
