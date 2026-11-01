using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Locations.Clients;

public interface ILocationLookupClient
{
    Task<LocationLookupDto?> GetAsync(double latitude, double longitude, CancellationToken cancellationToken);
}
