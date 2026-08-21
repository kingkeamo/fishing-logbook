using System.Net.Http.Json;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Localization;

namespace FishingLogBook.Web.Features.Profile.Clients;

public sealed class FishingPreferenceClient : IFishingPreferenceClient
{
    private readonly HttpClient _apiClient;
    private readonly ICatalogueLocalizer _catalogueLocalizer;

    public FishingPreferenceClient(
        IHttpClientFactory httpClientFactory,
        ICatalogueLocalizer catalogueLocalizer)
    {
        _apiClient = httpClientFactory.CreateClient(HttpClientNames.AuthorizedApi);
        _catalogueLocalizer = catalogueLocalizer;
    }

    public async Task<FishingCatalogueDto> GetCatalogueAsync(CancellationToken cancellationToken)
    {
        var catalogue = await _apiClient.GetFromJsonAsync<FishingCatalogueDto>(
            "api/fishing-catalogue",
            cancellationToken);
        return _catalogueLocalizer.Localize(
            catalogue ?? throw new HttpRequestException("Fishing catalogue was missing."));
    }

    public async Task<FishingPreferencesDto> GetPreferencesAsync(CancellationToken cancellationToken)
    {
        var preferences = await _apiClient.GetFromJsonAsync<FishingPreferencesDto>(
            "api/profiles/me/fishing-preferences",
            cancellationToken);
        return _catalogueLocalizer.Localize(
            preferences ?? throw new HttpRequestException("Fishing preferences were missing."));
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
        return _catalogueLocalizer.Localize(
            saved ?? throw new HttpRequestException("Saved fishing preferences were missing."));
    }

}
