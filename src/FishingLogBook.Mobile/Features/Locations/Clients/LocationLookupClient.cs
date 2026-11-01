using FishingLogBook.Mobile.Api.Clients;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Locations.Clients;

public sealed class LocationLookupClient(AuthorizedApiClient apiClient) : ILocationLookupClient
{
    private const string LabelPath = "api/locations/label";

    public async Task<LocationLookupDto?> GetAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var result = await apiClient.PostAsync<LocationLookupRequestDto, LocationLookupDto>(
            LabelPath,
            new LocationLookupRequestDto(latitude, longitude),
            cancellationToken);
        if (result.IsSuccess)
        {
            return result.Value;
        }

        if (result.Outcome is ApiOutcomeEnum.InvalidResponse or ApiOutcomeEnum.NotFound)
        {
            return null;
        }

        throw new InvalidOperationException("The location label lookup failed.");
    }
}
