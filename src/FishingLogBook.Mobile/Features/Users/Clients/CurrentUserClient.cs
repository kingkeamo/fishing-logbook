using System.Net.Http.Headers;
using System.Net.Http.Json;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Users.Clients;

public sealed class CurrentUserClient : ICurrentUserClient
{
    private readonly HttpClient _httpClient;

    public CurrentUserClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CurrentUserDto> GetCurrentAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/users/current");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CurrentUserDto>(cancellationToken)
            ?? throw new InvalidOperationException("The current user response was empty.");
    }
}
