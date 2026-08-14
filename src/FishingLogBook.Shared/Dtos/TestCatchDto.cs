namespace FishingLogBook.Shared.Dtos;

public sealed record TestCatchDto(
    Guid Id,
    string SpeciesName,
    DateTimeOffset CaughtOn,
    string? Notes);
