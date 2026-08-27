using System.Net.Http.Json;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Configuration;

namespace FishingLogBook.Web.Features.Profile.Clients;

public sealed class FishingLocationClient : IFishingLocationClient
{
    private const string ResourcePath = "api/profiles/me/fishing-locations";

    private readonly HttpClient _apiClient;

    public FishingLocationClient(IHttpClientFactory httpClientFactory)
    {
        _apiClient = httpClientFactory.CreateClient(HttpClientNames.AuthorizedApi);
    }

    public async Task<FishingLocationPreferencesDto> GetAsync(CancellationToken cancellationToken)
    {
        var locations = await _apiClient.GetFromJsonAsync<FishingLocationPreferencesDto>(
            ResourcePath,
            cancellationToken);
        return locations ?? throw new HttpRequestException("Fishing locations were missing.");
    }

    public async Task<FishingLocationPreferencesDto> UpdateAsync(
        UpdateFishingLocationPreferencesDto locations,
        CancellationToken cancellationToken)
    {
        using var response = await _apiClient.PutAsJsonAsync(ResourcePath, locations, cancellationToken);
        response.EnsureSuccessStatusCode();
        var saved = await response.Content.ReadFromJsonAsync<FishingLocationPreferencesDto>(cancellationToken);
        return saved ?? throw new HttpRequestException("Saved fishing locations were missing.");
    }
}
