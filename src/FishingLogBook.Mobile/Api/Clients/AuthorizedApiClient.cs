using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Api.Clients;

public sealed class AuthorizedApiClient
{
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private readonly HttpClient _httpClient;
    private readonly IAccountSessionCoordinator _sessionCoordinator;
    private readonly ILogger<AuthorizedApiClient> _logger;

    public AuthorizedApiClient(
        HttpClient httpClient,
        IAccountSessionCoordinator sessionCoordinator,
        ILogger<AuthorizedApiClient> logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = RequestTimeout;
        _sessionCoordinator = sessionCoordinator;
        _logger = logger;
    }

    public async Task<ApiResultModel<T>> GetAsync<T>(
        string path,
        CancellationToken cancellationToken,
        bool distinguishNotFound = false)
        where T : class
    {
        return await SendAsync<T>(new HttpRequestMessage(HttpMethod.Get, path), cancellationToken, distinguishNotFound);
    }

    public async Task<ApiResultModel<TResponse>> PutAsync<TRequest, TResponse>(
        string path,
        TRequest value,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = JsonContent.Create(value)
        };
        return await SendAsync<TResponse>(request, cancellationToken);
    }

    public async Task<ApiResultModel<TResponse>> PutAsync<TResponse>(
        string path,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path);
        return await SendAsync<TResponse>(request, cancellationToken);
    }

    public async Task<ApiResultModel<TResponse>> PostAsync<TRequest, TResponse>(
        string path,
        TRequest value,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(value)
        };
        return await SendAsync<TResponse>(request, cancellationToken, distinguishNotFound: true);
    }

    public async Task<ApiResultModel<NoContentModel>> PostNoContentAsync<TRequest>(
        string path,
        TRequest value,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(value)
        };
        return await SendAsync(request, cancellationToken, distinguishNotFound: true, ReadNoContent);
    }

    public async Task<ApiResultModel<NoContentModel>> DeleteAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, path);
        return await SendAsync(request, cancellationToken, distinguishNotFound: true, ReadNoContent);
    }

    private Task<ApiResultModel<T>> SendAsync<T>(
        HttpRequestMessage request,
        CancellationToken cancellationToken,
        bool distinguishNotFound = false)
        where T : class
    {
        return SendAsync(request, cancellationToken, distinguishNotFound, ReadJsonAsync<T>);
    }

    private async Task<ApiResultModel<T>> SendAsync<T>(
        HttpRequestMessage request,
        CancellationToken cancellationToken,
        bool distinguishNotFound,
        Func<HttpResponseMessage, CancellationToken, Task<ApiResultModel<T>>> readSuccess)
        where T : class
    {
        var lease = _sessionCoordinator.Capture();
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Authenticated API request returned {StatusCode}.", (int)response.StatusCode);
                return ApiResultModel<T>.Failed(MapFailureStatus(response.StatusCode, distinguishNotFound));
            }

            return await readSuccess(response, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return ApiResultModel<T>.Failed(ApiOutcomeEnum.SessionExpired);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or WebException or IOException or SocketException)
        {
            _logger.LogWarning("Authenticated API request failed with {ExceptionType}.", exception.GetType().FullName);
            return ApiResultModel<T>.Failed(ApiOutcomeEnum.Unavailable);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested &&
            lease is not null &&
            _sessionCoordinator.IsCurrent(lease))
        {
            _logger.LogWarning("Authenticated API request timed out.");
            return ApiResultModel<T>.Failed(ApiOutcomeEnum.Unavailable);
        }
    }

    private async Task<ApiResultModel<T>> ReadJsonAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
        where T : class
    {
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

    private static Task<ApiResultModel<NoContentModel>> ReadNoContent(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(ApiResultModel<NoContentModel>.Succeeded(NoContentModel.Instance));
    }

    private static ApiOutcomeEnum MapFailureStatus(HttpStatusCode statusCode, bool distinguishNotFound)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => ApiOutcomeEnum.ValidationFailed,
            HttpStatusCode.Unauthorized => ApiOutcomeEnum.SessionExpired,
            HttpStatusCode.Forbidden => ApiOutcomeEnum.Forbidden,
            HttpStatusCode.NotFound when distinguishNotFound => ApiOutcomeEnum.NotFound,
            HttpStatusCode.RequestTimeout => ApiOutcomeEnum.Unavailable,
            _ when (int)statusCode >= 500 => ApiOutcomeEnum.ServerError,
            _ => ApiOutcomeEnum.InvalidResponse
        };
    }
}
