using System.Net.Http.Json;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Configuration;

namespace FishingLogBook.Web.Features.OfflineAccess.Clients;

public sealed class OfflineAccessPreferenceClient : IOfflineAccessPreferenceClient
{
    private const string Path = "api/users/current/offline-access-preference";
    private readonly HttpClient _client;

    public OfflineAccessPreferenceClient(IHttpClientFactory factory) =>
        _client = factory.CreateClient(HttpClientNames.AuthorizedApi);

    public async Task<OfflineAccessPreferenceDto> GetAsync(CancellationToken cancellationToken) =>
        await _client.GetFromJsonAsync<OfflineAccessPreferenceDto>(Path, cancellationToken)
        ?? throw new HttpRequestException("Offline access preference was missing.");

    public async Task<OfflineAccessPreferenceDto> SetAsync(bool enabled, CancellationToken cancellationToken)
    {
        var response = await _client.PutAsJsonAsync(Path, new OfflineAccessPreferenceDto(enabled), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OfflineAccessPreferenceDto>(cancellationToken)
            ?? throw new HttpRequestException("Offline access preference was missing.");
    }
}
