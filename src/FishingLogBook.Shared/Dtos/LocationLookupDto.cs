namespace FishingLogBook.Shared.Dtos;

public sealed record LocationLookupDto(IReadOnlyList<string> Components)
{
    public string DisplayName => string.Join(", ", Components);

    public string? Street { get; init; }

    public string? VenueOrFacility { get; init; }

    public string? Locality { get; init; }

    public string? DistrictOrNeighbourhood { get; init; }

    public string? Region { get; init; }

    public string? Country { get; init; }
}
