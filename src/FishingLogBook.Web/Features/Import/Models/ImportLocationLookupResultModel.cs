namespace FishingLogBook.Web.Features.Import.Models;

public sealed record ImportLocationLookupResultModel(IReadOnlyList<string> Components)
{
    public string DisplayName => string.Join(", ", Components);

    public string? Street { get; init; }

    public string? VenueOrFacility { get; init; }

    public string? Locality { get; init; }

    public string? DistrictOrNeighbourhood { get; init; }

    public string? Region { get; init; }

    public string? Country { get; init; }
}
