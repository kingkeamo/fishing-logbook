using System.Net.Http.Headers;
using FishingLogBook.Mobile.Configuration;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Providers;

namespace FishingLogBook.Mobile.Features.Authentication.Handlers;

public sealed class ApiBearerHandler : DelegatingHandler
{
    private readonly Uri _apiBaseUri;
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly IAccountSessionCoordinator _sessionCoordinator;

    public ApiBearerHandler(
        MobileAuthConfig config,
        IAccessTokenProvider accessTokenProvider,
        IAccountSessionCoordinator sessionCoordinator)
    {
        _apiBaseUri = new Uri(config.ApiBaseUrl.TrimEnd('/') + "/");
        _accessTokenProvider = accessTokenProvider;
        _sessionCoordinator = sessionCoordinator;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is null || !IsApprovedEndpoint(request.RequestUri))
        {
            throw new InvalidOperationException("Authenticated requests may only target the configured API.");
        }

        var credential = await _accessTokenProvider.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.AccessToken);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            credential.SessionLease.CancellationToken);
        var response = await base.SendAsync(request, linkedCancellation.Token);
        if (!_sessionCoordinator.IsCurrent(credential.SessionLease))
        {
            response.Dispose();
            throw new OperationCanceledException("The account session changed while the request was running.");
        }

        return response;
    }

    private bool IsApprovedEndpoint(Uri requestUri)
    {
        if (!requestUri.IsAbsoluteUri ||
            requestUri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(requestUri.Scheme, _apiBaseUri.Scheme, StringComparison.Ordinal) ||
            !string.Equals(requestUri.Host, _apiBaseUri.Host, StringComparison.OrdinalIgnoreCase) ||
            requestUri.Port != _apiBaseUri.Port)
        {
            return false;
        }

        return requestUri.AbsolutePath.StartsWith(_apiBaseUri.AbsolutePath, StringComparison.Ordinal);
    }
}
