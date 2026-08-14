using System.Net.Http.Json;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Services;

public sealed class SystemStatusClient : ISystemStatusClient
{
    private readonly HttpClient _httpClient;

    public SystemStatusClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<HealthDto?> GetApiHealthAsync(CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<HealthDto>("health", cancellationToken);
    }

    public async Task<DatabaseTestDto?> GetDatabaseStatusAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync("api/system/database", cancellationToken);

        return await response.Content.ReadFromJsonAsync<DatabaseTestDto>(cancellationToken);
    }
}
