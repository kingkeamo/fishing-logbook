using System.Net.Http.Json;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Configuration;

namespace FishingLogBook.Web.Features.Users.Services;

public sealed class CurrentUserClient : ICurrentUserClient
{
    private readonly HttpClient _apiClient;

    public CurrentUserClient(IHttpClientFactory httpClientFactory)
    {
        _apiClient = httpClientFactory.CreateClient(HttpClientNames.AuthorizedApi);
    }

    public async Task<CurrentUserDto> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var current = await _apiClient.GetFromJsonAsync<CurrentUserDto>("api/users/current", cancellationToken);
        return current ?? throw new HttpRequestException("Current user was missing.");
    }
}
