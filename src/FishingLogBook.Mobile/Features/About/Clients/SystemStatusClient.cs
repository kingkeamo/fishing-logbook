using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.About.Clients;

public sealed class SystemStatusClient(HttpClient httpClient) : ISystemStatusClient
{
    public Task<ApiResultModel<HealthDto>> GetApiHealthAsync(CancellationToken cancellationToken)
    {
        return GetAsync<HealthDto>("health", cancellationToken);
    }

    public Task<ApiResultModel<DatabaseTestDto>> GetDatabaseStatusAsync(CancellationToken cancellationToken)
    {
        return GetAsync<DatabaseTestDto>("api/system/database", cancellationToken);
    }

    public Task<ApiResultModel<BuildMetadataDto>> GetBuildMetadataAsync(CancellationToken cancellationToken)
    {
        return GetAsync<BuildMetadataDto>("api/system/build", cancellationToken);
    }

    private async Task<ApiResultModel<T>> GetAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var response = await httpClient.GetAsync(path, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiResultModel<T>.Failed(MapFailure(response.StatusCode));
            }

            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            return value is null
                ? ApiResultModel<T>.Failed(ApiOutcomeEnum.InvalidResponse)
                : ApiResultModel<T>.Succeeded(value);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or SocketException)
        {
            return ApiResultModel<T>.Failed(ApiOutcomeEnum.Unavailable);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiResultModel<T>.Failed(ApiOutcomeEnum.Unavailable);
        }
    }

    private static ApiOutcomeEnum MapFailure(HttpStatusCode statusCode)
    {
        return (int)statusCode >= 500 ? ApiOutcomeEnum.ServerError : ApiOutcomeEnum.Unavailable;
    }
}
