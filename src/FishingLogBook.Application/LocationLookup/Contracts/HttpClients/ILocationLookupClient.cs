using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Application.LocationLookup.Contracts.HttpClients;

public interface ILocationLookupClient
{
    Task<LocationLookupDto?> ReverseGeocodeAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken);
}
