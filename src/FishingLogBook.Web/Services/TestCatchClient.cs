using System.Net.Http.Headers;
using System.Net.Http.Json;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Services;

public sealed class TestCatchClient : ITestCatchClient
{
    private readonly HttpClient _httpClient;

    public TestCatchClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task UpsertAsync(TestCatchDto testCatch, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/test-catches", testCatch, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<TestCatchDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var catches = await _httpClient.GetFromJsonAsync<IReadOnlyList<TestCatchDto>>(
            "api/test-catches",
            cancellationToken);

        return catches ?? [];
    }

    public async Task<PhotographUploadDto> CreatePhotographUploadAsync(
        Guid testCatchId,
        PhotographUploadRequestDto request,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/test-catches/{testCatchId:D}/photographs/upload-url",
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
        using var response = await _httpClient.PutAsync(uploadUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RecordPhotographAsync(
        Guid testCatchId,
        RecordPhotographDto request,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/test-catches/{testCatchId:D}/photographs",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
