namespace FishingLogBook.Shared.Dtos;

public sealed record TripCatchSummaryDto(Guid Id, DateTimeOffset CaughtOn)
{
    public string? SpeciesName { get; init; }

    public decimal? Weight { get; init; }

    public decimal? Length { get; init; }

    public string? PhotographUrl { get; init; }
}
