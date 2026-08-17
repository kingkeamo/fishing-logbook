namespace FishingLogBook.Shared.Dtos;

public sealed record CatchPhotographDto(
    Guid Id,
    Guid CatchId,
    string ContentType);
