using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Features.Catch.Clients;

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

    public async Task<IReadOnlyList<CatchViewDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var catches = await _apiClient.GetFromJsonAsync<IReadOnlyList<CatchViewDto>>(
            "api/catches",
            cancellationToken);
        return catches ?? [];
    }

    public async Task<CatchViewDto?> GetAsync(Guid catchId, CancellationToken cancellationToken)
    {
        using var response = await _apiClient.GetAsync($"api/catches/{catchId:D}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CatchViewDto>(cancellationToken);
    }

    public async Task<byte[]> DownloadPhotographAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _anonymousClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
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

    public async Task DeletePhotographAsync(
        Guid catchId,
        Guid photographId,
        CancellationToken cancellationToken)
    {
        using var response = await _apiClient.DeleteAsync(
            $"api/catches/{catchId:D}/photographs/{photographId:D}",
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

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

    public async Task<CatchAnglerCorrectionResult> CorrectAnglerAsync(
        Guid catchId,
        Guid anglerUserId,
        CancellationToken cancellationToken)
    {
        using var response = await _apiClient.PatchAsJsonAsync(
            $"api/catches/{catchId:D}/angler",
            new CorrectCatchAnglerDto(anglerUserId),
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new CatchAnglerCorrectionResult(null, null);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<CatchAnglerCorrectionErrorBody>(cancellationToken);
            return new CatchAnglerCorrectionResult(null, body?.ErrorMessage);
        }

        var updated = await response.Content.ReadFromJsonAsync<CatchViewDto>(cancellationToken);
        return new CatchAnglerCorrectionResult(updated, null);
    }

    private sealed record CatchAnglerCorrectionErrorBody(string? ErrorMessage);

    private static bool IsUnsynchronisedCatch(HttpResponseMessage response)
    {
        return response.StatusCode == HttpStatusCode.NotFound;
    }
}
