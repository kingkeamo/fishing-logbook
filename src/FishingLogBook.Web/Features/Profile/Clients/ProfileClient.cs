using System.Net.Http.Headers;
using System.Net.Http.Json;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Configuration;

namespace FishingLogBook.Web.Features.Profile.Clients;

public sealed class ProfileClient : IProfileClient
{
    private readonly HttpClient _apiClient;
    private readonly HttpClient _anonymousClient;

    public ProfileClient(IHttpClientFactory httpClientFactory)
    {
        _apiClient = httpClientFactory.CreateClient(HttpClientNames.AuthorizedApi);
        _anonymousClient = httpClientFactory.CreateClient(HttpClientNames.Anonymous);
    }

    public async Task<ProfileDto> GetOwnAsync(CancellationToken cancellationToken)
    {
        var profile = await _apiClient.GetFromJsonAsync<ProfileDto>("api/profiles/me", cancellationToken);
        return profile ?? throw new HttpRequestException("Own profile was missing.");
    }

    public async Task<ProfileDto> UpdateOwnAsync(UpdateProfileDto profile, CancellationToken cancellationToken)
    {
        using var response = await _apiClient.PutAsJsonAsync("api/profiles/me", profile, cancellationToken);
        response.EnsureSuccessStatusCode();
        var saved = await response.Content.ReadFromJsonAsync<ProfileDto>(cancellationToken);
        return saved ?? throw new HttpRequestException("Saved profile was missing.");
    }

    public async Task<ProfileDto> CompleteOnboardingAsync(CancellationToken cancellationToken)
    {
        using var response = await _apiClient.PutAsync("api/profiles/me/onboarding", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<ProfileDto>(cancellationToken);
        return profile ?? throw new HttpRequestException("Completed onboarding profile was missing.");
    }

    public async Task<PublicProfileDto> GetPublicAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _apiClient.GetFromJsonAsync<PublicProfileDto>(
            $"api/profiles/{userId:D}",
            cancellationToken);
        return profile ?? throw new HttpRequestException("Public profile was missing.");
    }

    public async Task<IReadOnlyList<AnglerSummaryDto>> FindAnglersAsync(
        string query,
        CancellationToken cancellationToken)
    {
        using var response = await _apiClient.GetAsync(
            $"api/profiles/lookup?q={Uri.EscapeDataString(query)}",
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var anglers = await response.Content.ReadFromJsonAsync<IReadOnlyList<AnglerSummaryDto>>(
            cancellationToken);
        return anglers ?? [];
    }

    public async Task<PhotographUploadDto> CreatePhotographUploadAsync(
        PhotographUploadRequestDto request,
        CancellationToken cancellationToken)
    {
        using var response = await _apiClient.PostAsJsonAsync(
            "api/profiles/me/photograph/upload-url",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var upload = await response.Content.ReadFromJsonAsync<PhotographUploadDto>(cancellationToken);
        return upload ?? throw new HttpRequestException("Photograph upload URL was missing.");
    }

    public async Task UploadPhotographAsync(
        string uploadUrl,
        byte[] bytes,
        string contentType,
        CancellationToken cancellationToken)
    {
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var response = await _anonymousClient.PutAsync(uploadUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ProfileDto> RecordPhotographAsync(
        RecordPhotographDto request,
        CancellationToken cancellationToken)
    {
        using var response = await _apiClient.PostAsJsonAsync(
            "api/profiles/me/photograph",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<ProfileDto>(cancellationToken);
        return profile ?? throw new HttpRequestException("Profile photograph was missing.");
    }
}
