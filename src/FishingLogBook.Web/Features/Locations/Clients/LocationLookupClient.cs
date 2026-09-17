using System.Net;
using System.Net.Http.Json;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Configuration;

namespace FishingLogBook.Web.Features.Locations.Clients;

public sealed class LocationLookupClient : ILocationLookupClient
{
    private readonly HttpClient _apiClient;

    public LocationLookupClient(IHttpClientFactory httpClientFactory)
    {
        _apiClient = httpClientFactory.CreateClient(HttpClientNames.AuthorizedApi);
    }

    public async Task<LocationLookupDto?> GetAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        using var response = await _apiClient.PostAsJsonAsync(
            "api/locations/label",
            new LocationLookupRequestDto(latitude, longitude),
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LocationLookupDto>(cancellationToken);
    }
}
