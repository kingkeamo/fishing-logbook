using System.Net.Http.Json;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Diagnostics;

public sealed class DiagnosticClient : IDiagnosticClient
{
    private readonly HttpClient _httpClient;

    public DiagnosticClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task UploadBatchAsync(IReadOnlyList<ClientDiagnosticEventDto> events, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/diagnostics/client",
            new ClientDiagnosticBatchDto { Events = events },
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
