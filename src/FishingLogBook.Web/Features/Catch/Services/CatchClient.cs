using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Configuration;

namespace FishingLogBook.Web.Features.Catch.Services;

public sealed class CatchClient : ICatchClient
{
    private readonly HttpClient _apiClient;
    private readonly HttpClient _anonymousClient;

    public CatchClient(IHttpClientFactory httpClientFactory)
    {
        _apiClient = httpClientFactory.CreateClient(HttpClientNames.AuthorizedApi);
        _anonymousClient = httpClientFactory.CreateClient(HttpClientNames.Anonymous);
    }

    public async Task UpsertAsync(CatchDto catchRecord, CancellationToken cancellationToken)
    {
        using var response = await _apiClient.PostAsJsonAsync(
            "api/catches",
            catchRecord,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<PhotographUploadDto> CreatePhotographUploadAsync(
        Guid catchId,
        PhotographUploadRequestDto request,
        CancellationToken cancellationToken)
    {
        using var response = await _apiClient.PostAsJsonAsync(
            $"api/catches/{catchId:D}/photographs/upload-url",
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

    public async Task RecordPhotographAsync(
        Guid catchId,
        RecordPhotographDto request,
        CancellationToken cancellationToken)
    {
        using var response = await _apiClient.PostAsJsonAsync(
            $"api/catches/{catchId:D}/photographs",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
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
        if (IsUnsynchronisedCatch(response))
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    private static bool IsUnsynchronisedCatch(HttpResponseMessage response)
    {
        return response.StatusCode == HttpStatusCode.NotFound;
    }
}
