using System.Net.Http.Json;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Configuration;

namespace FishingLogBook.Web.Features.Profile.Services;

public sealed class FishingPreferenceClient : IFishingPreferenceClient
{
    private readonly HttpClient _apiClient;

    public FishingPreferenceClient(IHttpClientFactory httpClientFactory)
    {
        _apiClient = httpClientFactory.CreateClient(HttpClientNames.AuthorizedApi);
    }

    public async Task<FishingCatalogueDto> GetCatalogueAsync(CancellationToken cancellationToken)
    {
        var catalogue = await _apiClient.GetFromJsonAsync<FishingCatalogueDto>(
            "api/fishing-catalogue",
            cancellationToken);
        return catalogue ?? throw new HttpRequestException("Fishing catalogue was missing.");
    }

    public async Task<FishingPreferencesDto> GetPreferencesAsync(CancellationToken cancellationToken)
    {
        var preferences = await _apiClient.GetFromJsonAsync<FishingPreferencesDto>(
            "api/profiles/me/fishing-preferences",
            cancellationToken);
        return preferences ?? throw new HttpRequestException("Fishing preferences were missing.");
    }

    public async Task<FishingPreferencesDto> UpdatePreferencesAsync(
        UpdateFishingPreferencesDto preferences,
        CancellationToken cancellationToken)
    {
        using var response = await _apiClient.PutAsJsonAsync(
            "api/profiles/me/fishing-preferences",
            preferences,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var saved = await response.Content.ReadFromJsonAsync<FishingPreferencesDto>(cancellationToken);
        return saved ?? throw new HttpRequestException("Saved fishing preferences were missing.");
    }
}
