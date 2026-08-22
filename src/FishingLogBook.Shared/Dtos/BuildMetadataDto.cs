namespace FishingLogBook.Shared.Dtos;

public sealed record BuildMetadataDto(
    string Version,
    string Sha,
    string Environment,
    DateTimeOffset? BuiltOn);
