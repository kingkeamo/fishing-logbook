using System.Net.Http.Headers;
using System.Net.Http.Json;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Configuration;

namespace FishingLogBook.Web.Features.Trips.Clients;

public sealed class TripClient : ITripClient
{
    private readonly HttpClient _apiClient;
    private readonly HttpClient _anonymousClient;

    public TripClient(IHttpClientFactory httpClientFactory)
    {
        _apiClient = httpClientFactory.CreateClient(HttpClientNames.AuthorizedApi);
        _anonymousClient = httpClientFactory.CreateClient(HttpClientNames.Anonymous);
    }

    public async Task<TripDto?> UpsertAsync(TripDto trip, CancellationToken cancellationToken)
    {
        using var response = await _apiClient.PostAsJsonAsync("api/trips", trip, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TripDto>(cancellationToken);
    }

    public async Task<PhotographUploadDto> CreatePhotographUploadAsync(
        Guid tripId,
        PhotographUploadRequestDto request,
        CancellationToken cancellationToken)
    {
        using var response = await _apiClient.PostAsJsonAsync(
            $"api/trips/{tripId:D}/photographs/upload-url",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var upload = await response.Content.ReadFromJsonAsync<PhotographUploadDto>(cancellationToken);
        return upload ?? throw new HttpRequestException("Trip photograph upload URL was missing.");
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

    public async Task<TripPhotographDto?> RecordPhotographAsync(
        Guid tripId,
        RecordTripPhotographDto request,
        CancellationToken cancellationToken)
    {
        using var response = await _apiClient.PostAsJsonAsync(
            $"api/trips/{tripId:D}/photographs",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TripPhotographDto>(cancellationToken);
    }

    public async Task DeletePhotographAsync(
        Guid tripId,
        Guid photographId,
        CancellationToken cancellationToken)
    {
        using var response = await _apiClient.DeleteAsync(
            $"api/trips/{tripId:D}/photographs/{photographId:D}",
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<TripNoteDto?> RecordNoteAsync(
        Guid tripId,
        RecordTripNoteDto request,
        CancellationToken cancellationToken)
    {
        using var response = await _apiClient.PostAsJsonAsync(
            $"api/trips/{tripId:D}/notes",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TripNoteDto>(cancellationToken);
    }

    public async Task DeleteNoteAsync(Guid tripId, Guid noteId, CancellationToken cancellationToken)
    {
        using var response = await _apiClient.DeleteAsync(
            $"api/trips/{tripId:D}/notes/{noteId:D}",
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
