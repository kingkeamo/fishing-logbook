using FishingLogBook.Infrastructure.HttpClients.Geoapify;
using FishingLogBook.Shared.Dtos;
using Mapster;

namespace FishingLogBook.Infrastructure.Persistence.Mappings;

public sealed class GeoapifyLocationMappingRegistration : IRegister
{
    void IRegister.Register(TypeAdapterConfig config)
    {
        config.NewConfig<GeoapifyLocationLookupClient.GeoapifyResult, LocationLookupDto>()
            .MapWith(source => Map(source)!);
    }

    private static LocationLookupDto? Map(GeoapifyLocationLookupClient.GeoapifyResult? result)
    {
        if (result is null)
        {
            return null;
        }

        var street = FirstPopulated(result.AddressLine1, result.Street);
        var venueOrFacility = FirstPopulated(result.Name, result.Suburb);
        var locality = FirstPopulated(result.City, result.Town, result.Village, result.Municipality, result.County);
        var districtOrNeighbourhood = FirstPopulated(result.District, result.Suburb, result.County);
        var region = FirstPopulated(result.State, result.County);
        var components = BuildComponents(
            result.Formatted,
            street,
            venueOrFacility,
            locality,
            districtOrNeighbourhood,
            region,
            result.Country);
        if (components.Count == 0)
        {
            return null;
        }

        return new LocationLookupDto(components)
        {
            Street = street,
            VenueOrFacility = venueOrFacility,
            Locality = locality,
            DistrictOrNeighbourhood = districtOrNeighbourhood,
            Region = region,
            Country = result.Country
        };
    }

    private static IReadOnlyList<string> BuildComponents(string? formatted, params string?[] fallback)
    {
        var values = string.IsNullOrWhiteSpace(formatted)
            ? fallback
            : formatted.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? FirstPopulated(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
