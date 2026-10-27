using FishingLogBook.Mobile.Features.Authentication.Browsers;
using FishingLogBook.Mobile.Features.Authentication.Clients;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Authentication.Stores;
using FishingLogBook.Mobile.Features.Users.Clients;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Authentication.Services;

public sealed class AuthenticationService : IAuthenticationService, IDisposable
{
    private static readonly TimeSpan RefreshWindow = TimeSpan.FromMinutes(1);
    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly IOidcProtocolClient _oidcClient;
    private readonly IAuthenticationBrowser _browser;
    private readonly ISecureAuthenticationStore _store;
    private readonly ICurrentUserClient _currentUserClient;
    private readonly ICognitoSessionClient _cognitoSessionClient;
    private readonly IAccountSessionCoordinator _sessionCoordinator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuthenticationService> _logger;
    private AuthenticationSessionModel? _currentSession;
    private bool _hasPendingAuthorization;

    public AuthenticationService(
        IOidcProtocolClient oidcClient,
        IAuthenticationBrowser browser,
        ISecureAuthenticationStore store,
        ICurrentUserClient currentUserClient,
        ICognitoSessionClient cognitoSessionClient,
        IAccountSessionCoordinator sessionCoordinator,
        TimeProvider timeProvider,
        ILogger<AuthenticationService> logger)
    {
        _oidcClient = oidcClient;
        _browser = browser;
        _store = store;
        _currentUserClient = currentUserClient;
        _cognitoSessionClient = cognitoSessionClient;
        _sessionCoordinator = sessionCoordinator;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public event EventHandler? SessionChanged;
    public event EventHandler<AuthenticationResultEnum>? SignInCompleted;

    public AuthenticationSessionModel? CurrentSession
    {
        get
        {
            lock (_stateLock)
            {
                return _currentSession;
            }
        }
    }

    public bool HasPendingAuthorization
    {
        get
        {
            lock (_stateLock)
            {
                return _hasPendingAuthorization;
            }
        }
    }

    public async Task<AuthenticationResultEnum> InitializeAsync(CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var pendingResult = await RestorePendingAuthorizationAsync();
            if (pendingResult == AuthenticationResultEnum.StorageUnavailable)
            {
                return pendingResult;
            }

            var credential = await _store.GetCredentialAsync();
            if (credential is null)
            {
                return AuthenticationResultEnum.Success;
            }

            return await RefreshAndEstablishAsync(credential, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Native authentication restoration failed.");
            await InvalidateLocalSessionAsync(removeCredential: false);
            return AuthenticationResultEnum.StorageUnavailable;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<AuthenticationResultEnum> BeginSignInAsync(CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            await _store.RemovePendingAuthorizationAsync();
            var pendingAuthorization = await _oidcClient.PrepareLoginAsync(cancellationToken);
            await _store.SavePendingAuthorizationAsync(pendingAuthorization);
            SetPendingAuthorization(true);
            await _browser.OpenAsync(new Uri(pendingAuthorization.StartUrl), cancellationToken);
            return AuthenticationResultEnum.Pending;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Native sign-in could not be started.");
            await RemovePendingAuthorizationSafelyAsync();
            return AuthenticationResultEnum.TemporarilyUnavailable;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<AuthenticationResultEnum> CompleteSignInAsync(
        Uri callback,
        CancellationToken cancellationToken)
    {
        var result = await CompleteSignInCoreAsync(callback, cancellationToken);
        if (result != AuthenticationResultEnum.Success)
        {
            _logger.LogWarning("Native sign-in callback completed with {Result}.", result);
        }

        SignInCompleted?.Invoke(this, result);
        return result;
    }

    public async Task CancelSignInAsync()
    {
        await _operationGate.WaitAsync();
        try
        {
            await RemovePendingAuthorizationSafelyAsync();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken);
        string? refreshToken;
        try
        {
            refreshToken = CurrentSession?.RefreshToken;
            await InvalidateLocalSessionAsync(removeCredential: true);
            await RemovePendingAuthorizationSafelyAsync();
        }
        finally
        {
            _operationGate.Release();
        }

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await TryRevokeAsync(refreshToken, cancellationToken);
        }

        await TryOpenLogoutAsync(cancellationToken);
    }

    public async Task<ApiAccessTokenModel> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var session = CurrentSession ?? throw new UnauthorizedAccessException("No authenticated session is active.");
            if (session.AccessTokenExpiresAt <= _timeProvider.GetUtcNow().Add(RefreshWindow))
            {
                var credential = new PersistedAuthenticationModel(
                    session.RefreshToken,
                    session.Subject,
                    session.UserId);
                var result = await RefreshAndEstablishAsync(credential, cancellationToken);
                if (result != AuthenticationResultEnum.Success)
                {
                    throw new UnauthorizedAccessException("The authenticated session could not be refreshed.");
                }

                session = CurrentSession ?? throw new UnauthorizedAccessException("No authenticated session is active.");
            }

            var lease = _sessionCoordinator.Capture()
                ?? throw new UnauthorizedAccessException("No account session is active.");
            return new ApiAccessTokenModel(session.AccessToken, lease);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public void Dispose()
    {
        _operationGate.Dispose();
    }

    private async Task<AuthenticationResultEnum> CompleteSignInCoreAsync(
        Uri callback,
        CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var pendingAuthorization = await _store.ConsumePendingAuthorizationAsync();
            SetPendingAuthorization(false);
            if (pendingAuthorization is null ||
                pendingAuthorization.ExpiresAt <= _timeProvider.GetUtcNow())
            {
                return AuthenticationResultEnum.Invalid;
            }

            var tokenResult = await _oidcClient.CompleteLoginAsync(
                callback,
                pendingAuthorization,
                cancellationToken);
            if (!tokenResult.IsSuccess)
            {
                return MapFailure(tokenResult.Failure);
            }

            return await EstablishSessionAsync(tokenResult, expectedCredential: null, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Native sign-in callback processing failed.");
            await InvalidateLocalSessionAsync(removeCredential: false);
            return AuthenticationResultEnum.Invalid;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task<AuthenticationResultEnum> RestorePendingAuthorizationAsync()
    {
        var pendingAuthorization = await _store.GetPendingAuthorizationAsync();
        if (pendingAuthorization is null)
        {
            SetPendingAuthorization(false);
            return AuthenticationResultEnum.Success;
        }

        if (pendingAuthorization.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            await _store.RemovePendingAuthorizationAsync();
            SetPendingAuthorization(false);
            return AuthenticationResultEnum.Invalid;
        }

        SetPendingAuthorization(true);
        return AuthenticationResultEnum.Pending;
    }

    private async Task<AuthenticationResultEnum> RefreshAndEstablishAsync(
        PersistedAuthenticationModel credential,
        CancellationToken cancellationToken)
    {
        var tokenResult = await _oidcClient.RefreshAsync(credential.RefreshToken, cancellationToken);
        if (!tokenResult.IsSuccess)
        {
            if (tokenResult.Failure == OidcFailureKindEnum.InvalidGrant)
            {
                await InvalidateLocalSessionAsync(removeCredential: true);
            }

            return MapFailure(tokenResult.Failure);
        }

        return await EstablishSessionAsync(tokenResult, credential, cancellationToken);
    }

    private async Task<AuthenticationResultEnum> EstablishSessionAsync(
        OidcTokenResultModel tokenResult,
        PersistedAuthenticationModel? expectedCredential,
        CancellationToken cancellationToken)
    {
        var subject = tokenResult.Subject ?? expectedCredential?.Subject;
        var refreshToken = tokenResult.RefreshToken ?? expectedCredential?.RefreshToken;
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(refreshToken))
        {
            await InvalidateLocalSessionAsync(removeCredential: false);
            return AuthenticationResultEnum.Invalid;
        }

        if (expectedCredential is not null &&
            !string.Equals(subject, expectedCredential.Subject, StringComparison.Ordinal))
        {
            await InvalidateLocalSessionAsync(removeCredential: true);
            return AuthenticationResultEnum.Invalid;
        }

        var currentUser = await _currentUserClient.GetCurrentAsync(tokenResult.AccessToken, cancellationToken);
        if (!string.Equals(currentUser.Subject, subject, StringComparison.Ordinal) ||
            expectedCredential is not null && currentUser.UserId != expectedCredential.UserId)
        {
            await InvalidateLocalSessionAsync(removeCredential: true);
            return AuthenticationResultEnum.Invalid;
        }

        var credential = new PersistedAuthenticationModel(refreshToken, subject, currentUser.UserId);
        try
        {
            await _store.SaveCredentialAsync(credential);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "The refreshed native credential could not be stored.");
            await InvalidateLocalSessionAsync(removeCredential: false);
            return AuthenticationResultEnum.StorageUnavailable;
        }

        await _sessionCoordinator.TransitionAsync(currentUser.UserId, CancellationToken.None);
        SetSession(new AuthenticationSessionModel(
            tokenResult.AccessToken,
            tokenResult.AccessTokenExpiresAt,
            refreshToken,
            subject,
            tokenResult.Email ?? currentUser.Email,
            currentUser.UserId));
        return AuthenticationResultEnum.Success;
    }

    private async Task InvalidateLocalSessionAsync(bool removeCredential)
    {
        SetSession(null);
        await _sessionCoordinator.TransitionAsync(userId: null, CancellationToken.None);
        if (!removeCredential)
        {
            return;
        }

        try
        {
            await _store.RemoveCredentialAsync();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "The native credential could not be removed during local sign-out.");
        }
    }

    private async Task RemovePendingAuthorizationSafelyAsync()
    {
        SetPendingAuthorization(false);
        try
        {
            await _store.RemovePendingAuthorizationAsync();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "The pending native authorization could not be removed.");
        }
    }

    private async Task TryRevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        try
        {
            await _cognitoSessionClient.RevokeRefreshTokenAsync(refreshToken, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Remote native credential revocation failed after local sign-out.");
        }
    }

    private async Task TryOpenLogoutAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _cognitoSessionClient.OpenLogoutAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Remote Cognito logout failed after local sign-out.");
        }
    }

    private void SetSession(AuthenticationSessionModel? session)
    {
        bool changed;
        lock (_stateLock)
        {
            changed = (_currentSession is null) != (session is null) ||
                _currentSession?.UserId != session?.UserId ||
                !string.Equals(_currentSession?.Email, session?.Email, StringComparison.Ordinal);
            _currentSession = session;
        }

        if (changed)
        {
            SessionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SetPendingAuthorization(bool hasPendingAuthorization)
    {
        lock (_stateLock)
        {
            _hasPendingAuthorization = hasPendingAuthorization;
        }
    }

    private static AuthenticationResultEnum MapFailure(OidcFailureKindEnum failure)
    {
        return failure switch
        {
            OidcFailureKindEnum.Cancelled => AuthenticationResultEnum.Cancelled,
            OidcFailureKindEnum.InvalidResponse => AuthenticationResultEnum.Invalid,
            OidcFailureKindEnum.InvalidGrant => AuthenticationResultEnum.Invalid,
            _ => AuthenticationResultEnum.TemporarilyUnavailable
        };
    }
}
