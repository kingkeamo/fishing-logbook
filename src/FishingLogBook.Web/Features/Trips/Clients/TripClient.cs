using System.Net.Http.Json;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Configuration;

namespace FishingLogBook.Web.Features.Trips.Clients;

public sealed class TripClient : ITripClient
{
    private readonly HttpClient _apiClient;

    public TripClient(IHttpClientFactory httpClientFactory)
    {
        _apiClient = httpClientFactory.CreateClient(HttpClientNames.AuthorizedApi);
    }

    public async Task<TripDto?> UpsertAsync(TripDto trip, CancellationToken cancellationToken)
    {
        using var response = await _apiClient.PostAsJsonAsync("api/trips", trip, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TripDto>(cancellationToken);
    }
}
