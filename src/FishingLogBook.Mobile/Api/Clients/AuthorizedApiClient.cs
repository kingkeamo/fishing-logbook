using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Api.Clients;

public sealed class AuthorizedApiClient
{
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthorizedApiClient> _logger;

    public AuthorizedApiClient(HttpClient httpClient, ILogger<AuthorizedApiClient> logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = RequestTimeout;
        _logger = logger;
    }

    public async Task<ApiResultModel<T>> GetAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var response = await _httpClient.GetAsync(path, cancellationToken);
            return await ReadAsync<T>(response, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return ApiResultModel<T>.Failed(ApiOutcomeEnum.SessionExpired);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning("Authenticated API request failed with {ExceptionType}.", exception.GetType().FullName);
            return ApiResultModel<T>.Failed(ApiOutcomeEnum.Unavailable);
        }
        catch (TaskCanceledException exception) when (
            !cancellationToken.IsCancellationRequested &&
            exception.InnerException is TimeoutException)
        {
            _logger.LogWarning("Authenticated API request timed out.");
            return ApiResultModel<T>.Failed(ApiOutcomeEnum.Unavailable);
        }
    }

    private async Task<ApiResultModel<T>> ReadAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
        where T : class
    {
        if (!response.IsSuccessStatusCode)
        {
            var outcome = MapFailureStatus(response.StatusCode);
            _logger.LogWarning("Authenticated API request returned {StatusCode}.", (int)response.StatusCode);
            return ApiResultModel<T>.Failed(outcome);
        }

        try
        {
            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            return value is null
                ? ApiResultModel<T>.Failed(ApiOutcomeEnum.InvalidResponse)
                : ApiResultModel<T>.Succeeded(value);
        }
        catch (JsonException)
        {
            _logger.LogWarning("Authenticated API response could not be read.");
            return ApiResultModel<T>.Failed(ApiOutcomeEnum.InvalidResponse);
        }
    }

    private static ApiOutcomeEnum MapFailureStatus(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => ApiOutcomeEnum.ValidationFailed,
            HttpStatusCode.Unauthorized => ApiOutcomeEnum.SessionExpired,
            HttpStatusCode.Forbidden => ApiOutcomeEnum.Forbidden,
            HttpStatusCode.RequestTimeout => ApiOutcomeEnum.Unavailable,
            _ when (int)statusCode >= 500 => ApiOutcomeEnum.ServerError,
            _ => ApiOutcomeEnum.InvalidResponse
        };
    }
}
