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
}
