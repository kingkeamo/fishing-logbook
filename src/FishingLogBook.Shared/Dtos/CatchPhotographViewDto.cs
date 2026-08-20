namespace FishingLogBook.Shared.Dtos;

public sealed record CatchPhotographViewDto(
    Guid Id,
    string ContentType,
    string? Url);
