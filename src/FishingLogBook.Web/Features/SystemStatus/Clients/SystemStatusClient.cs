using System.Net.Http.Headers;
using System.Net.Http.Json;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Features.SystemStatus.Clients;

public sealed class SystemStatusClient : ISystemStatusClient
{
    private static readonly TimeSpan ReachabilityTimeout = TimeSpan.FromSeconds(3);

    private readonly HttpClient _httpClient;

    public SystemStatusClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<HealthDto?> GetApiHealthAsync(CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<HealthDto>("health", cancellationToken);
    }

    public async Task<bool> IsApiReachableAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ReachabilityTimeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "health");
            request.Headers.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true
            };
            request.Headers.Pragma.Add(new NameValueHeaderValue("no-cache"));
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            return true;
        }
        catch (HttpRequestException exception) when (exception.StatusCode is not null)
        {
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public async Task<DatabaseTestDto?> GetDatabaseStatusAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync("api/system/database", cancellationToken);

        return await response.Content.ReadFromJsonAsync<DatabaseTestDto>(cancellationToken);
    }

    public async Task<BuildMetadataDto?> GetBuildMetadataAsync(CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<BuildMetadataDto>("api/system/build", cancellationToken);
    }
}
