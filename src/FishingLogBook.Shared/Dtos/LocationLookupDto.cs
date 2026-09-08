namespace FishingLogBook.Shared.Dtos;

public sealed record LocationLookupDto(
    string DisplayName,
    string? Locality,
    string? Region,
    string? Country);
