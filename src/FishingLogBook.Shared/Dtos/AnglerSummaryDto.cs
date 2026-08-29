namespace FishingLogBook.Shared.Dtos;

public sealed record AnglerSummaryDto(
    Guid UserId,
    string? DisplayName,
    string? PhotographUrl,
    string? HomeRegion);
