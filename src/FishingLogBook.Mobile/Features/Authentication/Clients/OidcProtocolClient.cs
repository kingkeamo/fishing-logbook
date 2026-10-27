using System.Security.Claims;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient;
using FishingLogBook.Mobile.Configuration;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Mobile.Features.Authentication.Clients;

public sealed class OidcProtocolClient : IOidcProtocolClient
{
    private static readonly TimeSpan PendingAuthorizationLifetime = TimeSpan.FromMinutes(5);
    private static readonly HashSet<string> PermanentRefreshErrors = new(StringComparer.Ordinal)
    {
        "invalid_client",
        "invalid_request",
        "invalid_scope",
        "unauthorized_client",
        "unsupported_grant_type"
    };
    private readonly MobileAuthConfig _config;
    private readonly OidcClient _client;
    private readonly TimeProvider _timeProvider;

    public OidcProtocolClient(
        MobileAuthConfig config,
        TimeProvider timeProvider,
        HttpMessageHandler backchannelHandler)
    {
        _config = config;
        _timeProvider = timeProvider;
        _client = new OidcClient(new OidcClientOptions
        {
            Authority = config.Authority,
            ClientId = config.ClientId,
            RedirectUri = config.RedirectUri,
            Scope = $"openid email profile {config.ApiScope}",
            LoadProfile = false,
            DisablePushedAuthorization = true,
            BackchannelHandler = backchannelHandler,
            Policy = new Policy
            {
                Discovery = new DiscoveryPolicy
                {
                    AdditionalEndpointBaseAddresses = { config.HostedUiDomain }
                }
            },
            LoggerFactory = NullLoggerFactory.Instance
        });
    }

    public async Task<PendingAuthorizationModel> PrepareLoginAsync(CancellationToken cancellationToken)
    {
        var parameters = new Parameters
        {
            { "resource", _config.ApiResource }
        };
        var state = await _client.PrepareLoginAsync(parameters, cancellationToken);
        if (state.IsError)
        {
            throw new InvalidOperationException("The OIDC authorization request could not be prepared.");
        }

        return new PendingAuthorizationModel(
            state.StartUrl,
            state.State,
            state.CodeVerifier,
            state.RedirectUri,
            _timeProvider.GetUtcNow().Add(PendingAuthorizationLifetime));
    }

    public async Task<OidcTokenResultModel> CompleteLoginAsync(
        Uri callback,
        PendingAuthorizationModel pendingAuthorization,
        CancellationToken cancellationToken)
    {
        if (!IsExpectedCallback(callback))
        {
            return OidcTokenResultModel.Failed(OidcFailureKindEnum.InvalidResponse);
        }

        var response = callback.Query.TrimStart('?');
        var authorizeResponse = new AuthorizeResponse(response);
        if (!string.Equals(authorizeResponse.State, pendingAuthorization.State, StringComparison.Ordinal))
        {
            return OidcTokenResultModel.Failed(OidcFailureKindEnum.InvalidResponse);
        }

        var state = new AuthorizeState
        {
            StartUrl = pendingAuthorization.StartUrl,
            State = pendingAuthorization.State,
            CodeVerifier = pendingAuthorization.CodeVerifier,
            RedirectUri = pendingAuthorization.RedirectUri
        };
        var result = await _client.ProcessResponseAsync(response, state, cancellationToken: cancellationToken);
        if (result.IsError)
        {
            var failure = string.Equals(result.Error, "access_denied", StringComparison.Ordinal)
                ? OidcFailureKindEnum.Cancelled
                : OidcFailureKindEnum.InvalidResponse;
            return OidcTokenResultModel.Failed(failure);
        }

        return CreateResult(
            result.AccessToken,
            result.RefreshToken,
            result.AccessTokenExpiration,
            result.User);
    }

    public async Task<OidcTokenResultModel> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _client.RefreshTokenAsync(refreshToken, cancellationToken: cancellationToken);
            if (result.IsError)
            {
                return OidcTokenResultModel.Failed(ClassifyRefreshError(result.Error));
            }

            if (string.IsNullOrWhiteSpace(result.AccessToken))
            {
                return OidcTokenResultModel.Failed(OidcFailureKindEnum.InvalidResponse);
            }

            return new OidcTokenResultModel(
                OidcFailureKindEnum.None,
                result.AccessToken,
                result.RefreshToken,
                result.AccessTokenExpiration,
                null,
                null);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            return OidcTokenResultModel.Failed(OidcFailureKindEnum.Transient);
        }
    }

    private static OidcFailureKindEnum ClassifyRefreshError(string? error)
    {
        if (string.Equals(error, "invalid_grant", StringComparison.Ordinal))
        {
            return OidcFailureKindEnum.InvalidGrant;
        }

        return error is not null && PermanentRefreshErrors.Contains(error)
            ? OidcFailureKindEnum.Rejected
            : OidcFailureKindEnum.Transient;
    }

    private static OidcTokenResultModel CreateResult(
        string accessToken,
        string? refreshToken,
        DateTimeOffset expiration,
        ClaimsPrincipal user)
    {
        var subject = user.FindFirst("sub")?.Value;
        var email = user.FindFirst("email")?.Value;
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(subject))
        {
            return OidcTokenResultModel.Failed(OidcFailureKindEnum.InvalidResponse);
        }

        return new OidcTokenResultModel(
            OidcFailureKindEnum.None,
            accessToken,
            refreshToken,
            expiration,
            subject,
            email);
    }

    private bool IsExpectedCallback(Uri callback)
    {
        var expected = new Uri(_config.RedirectUri);
        return string.Equals(callback.Scheme, expected.Scheme, StringComparison.Ordinal) &&
            string.Equals(callback.Host, expected.Host, StringComparison.Ordinal) &&
            callback.Port == expected.Port &&
            string.Equals(callback.AbsolutePath, expected.AbsolutePath, StringComparison.Ordinal);
    }
}
