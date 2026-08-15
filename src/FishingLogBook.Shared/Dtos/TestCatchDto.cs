namespace FishingLogBook.Shared.Dtos;

public sealed record TestCatchDto(
    Guid Id,
    string SpeciesName,
    DateTimeOffset CaughtOn,
    string? Notes,
    Guid? PhotographId = null,
    string? PhotographContentType = null,
    string? PhotographUrl = null,
    CatchLocationDto? Location = null);
