using System.Net.Http.Json;
using FishingLogBook.Shared.SystemStatus;

namespace FishingLogBook.Web.Services;

public sealed class SystemStatusClient : ISystemStatusClient
{
    private readonly HttpClient _httpClient;

    public SystemStatusClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<HealthResponse?> GetApiHealthAsync(CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<HealthResponse>("health", cancellationToken);
    }

    public async Task<DatabaseTestResponse?> GetDatabaseStatusAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync("api/system/database", cancellationToken);

        return await response.Content.ReadFromJsonAsync<DatabaseTestResponse>(cancellationToken);
    }
}
