using System.Net;
using System.Net.Http.Json;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Configuration;

namespace FishingLogBook.Web.Features.Catch.Services;

public sealed class CatchClient : ICatchClient
{
    private readonly HttpClient _apiClient;

    public CatchClient(IHttpClientFactory httpClientFactory)
    {
        _apiClient = httpClientFactory.CreateClient(HttpClientNames.AuthorizedApi);
    }

    public async Task UpdateLocationVisibilityAsync(
        Guid catchId,
        string visibility,
        CancellationToken cancellationToken)
    {
        using var response = await _apiClient.PatchAsJsonAsync(
            $"api/catches/{catchId:D}/location-visibility",
            new UpdateCatchLocationVisibilityDto(visibility),
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }
}
