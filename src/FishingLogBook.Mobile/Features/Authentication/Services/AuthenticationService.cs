using FishingLogBook.Mobile.Features.Authentication.Browsers;
using FishingLogBook.Mobile.Features.Authentication.Clients;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Authentication.Stores;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Services;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Users.Clients;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Authentication.Services;

public sealed class AuthenticationService : IAuthenticationService, IDisposable
{
    private static readonly TimeSpan RefreshWindow = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan InitialRefreshBackoff = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MaximumRefreshBackoff = TimeSpan.FromMinutes(5);
    private const int MaximumBackoffDoublings = 5;
    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly IOidcProtocolClient _oidcClient;
    private readonly IOidcRefreshService _refreshService;
    private readonly IAuthenticationBrowser _browser;
    private readonly ISecureAuthenticationStore _store;
    private readonly ICurrentUserClient _currentUserClient;
    private readonly ICognitoSessionClient _cognitoSessionClient;
    private readonly IAccountSessionCoordinator _sessionCoordinator;
    private readonly IOfflineCapabilityService _offlineCapability;
    private readonly IOfflineSessionService _offlineSession;
    private readonly IConnectivityService _connectivity;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuthenticationService> _logger;
    private AuthenticationSessionModel? _currentSession;
    private bool _hasPendingAuthorization;
    private int _consecutiveRefreshFailures;
    private DateTimeOffset _refreshRetryAfter = DateTimeOffset.MinValue;
    private bool _signOutRequested;

    public AuthenticationService(
        IOidcProtocolClient oidcClient,
        IOidcRefreshService refreshService,
        IAuthenticationBrowser browser,
        ISecureAuthenticationStore store,
        ICurrentUserClient currentUserClient,
        ICognitoSessionClient cognitoSessionClient,
        IAccountSessionCoordinator sessionCoordinator,
        IOfflineCapabilityService offlineCapability,
        IOfflineSessionService offlineSession,
        IConnectivityService connectivity,
        TimeProvider timeProvider,
        ILogger<AuthenticationService> logger)
    {
        _oidcClient = oidcClient;
        _refreshService = refreshService;
        _browser = browser;
        _store = store;
        _currentUserClient = currentUserClient;
        _cognitoSessionClient = cognitoSessionClient;
        _sessionCoordinator = sessionCoordinator;
        _offlineCapability = offlineCapability;
        _offlineSession = offlineSession;
        _connectivity = connectivity;
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
        OfflineRecoveryContext? recovery = null;
        try
        {
            var offlineCapability = _offlineSession.Current;
            recovery = CaptureOfflineRecovery(offlineCapability);
            if (offlineCapability is not null && recovery is null)
            {
                return AuthenticationResultEnum.Cancelled;
            }

            var pendingResult = await RestorePendingAuthorizationAsync();
            if (pendingResult != AuthenticationResultEnum.Success)
            {
                return pendingResult;
            }

            var credential = await _store.GetCredentialAsync();
            if (credential is null)
            {
                if (recovery is not null)
                {
                    await InvalidateRecoveredAccountAsync(recovery);
                    return AuthenticationResultEnum.Invalid;
                }

                return AuthenticationResultEnum.Success;
            }

            if (recovery is not null &&
                (!string.Equals(credential.Subject, recovery.Capability.Subject, StringComparison.Ordinal) ||
                    credential.UserId != recovery.Capability.UserId))
            {
                await InvalidateRecoveredAccountAsync(recovery);
                return AuthenticationResultEnum.Invalid;
            }

            if (_timeProvider.GetUtcNow() < _refreshRetryAfter)
            {
                return AuthenticationResultEnum.TemporarilyUnavailable;
            }

            return await RefreshAndEstablishAsync(credential, recovery, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Native authentication restoration failed.");
            if (recovery is not null)
            {
                RecordRefreshFailure();
                return AuthenticationResultEnum.StorageUnavailable;
            }

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
            await _browser.OpenAsync(
                new Uri(pendingAuthorization.StartUrl),
                new Uri(pendingAuthorization.RedirectUri),
                cancellationToken);
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
        bool wasPending;
        await _operationGate.WaitAsync();
        try
        {
            wasPending = HasPendingAuthorization;
            await RemovePendingAuthorizationSafelyAsync();
        }
        finally
        {
            _operationGate.Release();
        }

        if (wasPending)
        {
            SignInCompleted?.Invoke(this, AuthenticationResultEnum.Cancelled);
        }
    }

    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        SetSignOutRequested(true);
        try
        {
            await _operationGate.WaitAsync(cancellationToken);
            string? refreshToken;
            try
            {
                refreshToken = CurrentSession?.RefreshToken;
                await InvalidateLocalSessionAsync(removeCredential: true, revokeOfflineCapability: true);
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
        finally
        {
            SetSignOutRequested(false);
        }
    }

    public async Task<ApiAccessTokenModel> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var session = CurrentSession ?? throw new UnauthorizedAccessException("No authenticated session is active.");
            var now = _timeProvider.GetUtcNow();
            if (session.AccessTokenExpiresAt <= now.Add(RefreshWindow) && now >= _refreshRetryAfter)
            {
                await TryRefreshSessionAsync(session, cancellationToken);
                session = CurrentSession ?? throw new UnauthorizedAccessException("No authenticated session is active.");
            }

            if (session.AccessTokenExpiresAt <= _timeProvider.GetUtcNow())
            {
                throw new UnauthorizedAccessException("The authenticated session could not be refreshed.");
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

            return await EstablishSessionAsync(
                tokenResult,
                expectedCredential: null,
                recovery: null,
                cancellationToken);
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
            return AuthenticationResultEnum.Success;
        }

        SetPendingAuthorization(true);
        return AuthenticationResultEnum.Pending;
    }

    private async Task<AuthenticationResultEnum> RefreshAndEstablishAsync(
        PersistedAuthenticationModel credential,
        OfflineRecoveryContext? recovery,
        CancellationToken cancellationToken)
    {
        var tokenResult = await _refreshService.RefreshAsync(credential.RefreshToken, cancellationToken);
        if (!tokenResult.IsSuccess)
        {
            if (IsPermanentRefreshFailure(tokenResult.Failure))
            {
                if (recovery is null)
                {
                    await InvalidateLocalSessionAsync(removeCredential: true, revokeOfflineCapability: true);
                }
                else
                {
                    await InvalidateRecoveredAccountAsync(recovery);
                }
            }
            else
            {
                RecordRefreshFailure();
            }

            return MapFailure(tokenResult.Failure);
        }

        return await EstablishSessionAsync(tokenResult, credential, recovery, cancellationToken);
    }

    private async Task TryRefreshSessionAsync(
        AuthenticationSessionModel session,
        CancellationToken cancellationToken)
    {
        var credential = new PersistedAuthenticationModel(
            session.RefreshToken,
            session.Subject,
            session.UserId);
        try
        {
            await RefreshAndEstablishAsync(credential, recovery: null, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                "Native session refresh failed with {ExceptionType}.",
                exception.GetType().FullName);
            RecordRefreshFailure();
        }
    }

    private void RecordRefreshFailure()
    {
        _consecutiveRefreshFailures++;
        var doublings = Math.Min(_consecutiveRefreshFailures - 1, MaximumBackoffDoublings);
        var backoff = TimeSpan.FromTicks(Math.Min(
            InitialRefreshBackoff.Ticks << doublings,
            MaximumRefreshBackoff.Ticks));
        _refreshRetryAfter = _timeProvider.GetUtcNow().Add(backoff);
    }

    private void ResetRefreshBackoff()
    {
        _consecutiveRefreshFailures = 0;
        _refreshRetryAfter = DateTimeOffset.MinValue;
    }

    private static bool IsPermanentRefreshFailure(OidcFailureKindEnum failure)
    {
        return failure is OidcFailureKindEnum.InvalidGrant or
            OidcFailureKindEnum.InvalidResponse or
            OidcFailureKindEnum.Rejected;
    }

    private async Task<AuthenticationResultEnum> EstablishSessionAsync(
        OidcTokenResultModel tokenResult,
        PersistedAuthenticationModel? expectedCredential,
        OfflineRecoveryContext? recovery,
        CancellationToken cancellationToken)
    {
        var subject = tokenResult.Subject ?? expectedCredential?.Subject;
        var refreshToken = tokenResult.RefreshToken ?? expectedCredential?.RefreshToken;
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(refreshToken))
        {
            await InvalidateRejectedSessionAsync(recovery);
            return AuthenticationResultEnum.Invalid;
        }

        if (expectedCredential is not null &&
            !string.Equals(subject, expectedCredential.Subject, StringComparison.Ordinal))
        {
            await InvalidateRejectedSessionAsync(recovery);
            return AuthenticationResultEnum.Invalid;
        }

        var currentUser = await _currentUserClient.GetCurrentAsync(tokenResult.AccessToken, cancellationToken);
        if (!string.Equals(currentUser.Subject, subject, StringComparison.Ordinal) ||
            expectedCredential is not null && currentUser.UserId != expectedCredential.UserId)
        {
            await InvalidateRejectedSessionAsync(recovery);
            return AuthenticationResultEnum.Invalid;
        }

        var credential = new PersistedAuthenticationModel(refreshToken, subject, currentUser.UserId);
        if (recovery is not null)
        {
            return await CommitRecoveredSessionAsync(
                recovery,
                credential,
                tokenResult,
                currentUser.Email,
                cancellationToken);
        }

        try
        {
            await _offlineCapability.RevokeIfOwnedByAnotherAccountAsync(
                subject,
                currentUser.UserId,
                cancellationToken);
            await _store.SaveCredentialAsync(credential);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The refreshed native credential could not be stored.");
            await InvalidateLocalSessionAsync(removeCredential: false);
            return AuthenticationResultEnum.StorageUnavailable;
        }

        var offlineWasActive = _offlineSession.Lock();
        await _sessionCoordinator.TransitionAsync(currentUser.UserId, CancellationToken.None);
        ResetRefreshBackoff();
        SetSession(new AuthenticationSessionModel(
            tokenResult.AccessToken,
            tokenResult.AccessTokenExpiresAt,
            refreshToken,
            subject,
            tokenResult.Email ?? currentUser.Email,
            currentUser.UserId), offlineWasActive);
        return AuthenticationResultEnum.Success;
    }

    private OfflineRecoveryContext? CaptureOfflineRecovery(OfflineCapabilityModel? capability)
    {
        if (capability is null)
        {
            return null;
        }

        var lease = _sessionCoordinator.Capture();
        return lease is not null && lease.UserId == capability.UserId
            ? new OfflineRecoveryContext(capability, lease)
            : null;
    }

    private async Task InvalidateRejectedSessionAsync(OfflineRecoveryContext? recovery)
    {
        if (recovery is null)
        {
            await InvalidateLocalSessionAsync(removeCredential: true, revokeOfflineCapability: true);
            return;
        }

        await InvalidateRecoveredAccountAsync(recovery);
    }

    private async Task InvalidateRecoveredAccountAsync(OfflineRecoveryContext recovery)
    {
        Exception? revocationFailure = null;
        try
        {
            await _offlineCapability.RevokeIfOwnedByAsync(
                recovery.Capability.Subject,
                recovery.Capability.UserId,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            revocationFailure = exception;
        }

        if (_sessionCoordinator.IsCurrent(recovery.Lease) &&
            _offlineSession.Current == recovery.Capability)
        {
            _offlineSession.Lock();
            SetSession(null, forceChanged: true);
            await _sessionCoordinator.TransitionAsync(userId: null, CancellationToken.None);
        }

        await RemoveCredentialIfOwnedByAsync(recovery.Capability);

        if (revocationFailure is not null)
        {
            throw new InvalidOperationException(
                "Offline access could not be revoked after authentication rejection.",
                revocationFailure);
        }
    }

    private async Task<AuthenticationResultEnum> CommitRecoveredSessionAsync(
        OfflineRecoveryContext recovery,
        PersistedAuthenticationModel credential,
        OidcTokenResultModel tokenResult,
        string currentUserEmail,
        CancellationToken cancellationToken)
    {
        if (!IsRecoveryOwnerCurrent(recovery, credential))
        {
            return AuthenticationResultEnum.Cancelled;
        }

        var ownerWasCurrent = true;
        var sessionPublished = false;
        try
        {
            var committed = await _sessionCoordinator.TryCommitAsync(
                recovery.Lease,
                async token =>
                {
                    token.ThrowIfCancellationRequested();
                    if (!IsRecoveryOwnerCurrent(recovery, credential))
                    {
                        ownerWasCurrent = false;
                        return;
                    }

                    await _store.SaveCredentialAsync(credential);
                    token.ThrowIfCancellationRequested();
                    if (!IsRecoveryOwnerCurrent(recovery, credential))
                    {
                        ownerWasCurrent = false;
                        return;
                    }

                    var stillOnline = _connectivity.TryRunWhileOnline(() =>
                    {
                        token.ThrowIfCancellationRequested();
                        if (!IsRecoveryOwnerCurrent(recovery, credential))
                        {
                            ownerWasCurrent = false;
                            return;
                        }

                        _offlineSession.Lock();
                        ResetRefreshBackoff();
                        SetSession(new AuthenticationSessionModel(
                            tokenResult.AccessToken,
                            tokenResult.AccessTokenExpiresAt,
                            credential.RefreshToken,
                            credential.Subject,
                            tokenResult.Email ?? currentUserEmail,
                            credential.UserId), forceChanged: true);
                        sessionPublished = true;
                    });

                    if (sessionPublished && !stillOnline)
                    {
                        SetSession(null, forceChanged: true);
                        _offlineSession.Activate(recovery.Capability);
                        sessionPublished = false;
                    }
                },
                cancellationToken);
            return committed && ownerWasCurrent && sessionPublished
                ? AuthenticationResultEnum.Success
                : AuthenticationResultEnum.Cancelled;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The recovered native credential could not be stored.");
            return AuthenticationResultEnum.StorageUnavailable;
        }
    }

    private async Task RemoveCredentialIfOwnedByAsync(OfflineCapabilityModel capability)
    {
        try
        {
            var credential = await _store.GetCredentialAsync();
            if (credential is null ||
                !string.Equals(credential.Subject, capability.Subject, StringComparison.Ordinal) ||
                credential.UserId != capability.UserId)
            {
                return;
            }

            await _store.RemoveCredentialAsync();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "The rejected native credential could not be removed.");
        }
    }

    private bool IsRecoveryOwnerCurrent(
        OfflineRecoveryContext recovery,
        PersistedAuthenticationModel credential)
    {
        return _sessionCoordinator.IsCurrent(recovery.Lease) &&
            !IsSignOutRequested() &&
            _offlineSession.Current == recovery.Capability &&
            recovery.Capability.IsDeviceReady &&
            string.Equals(credential.Subject, recovery.Capability.Subject, StringComparison.Ordinal) &&
            credential.UserId == recovery.Capability.UserId;
    }

    private bool IsSignOutRequested()
    {
        lock (_stateLock)
        {
            return _signOutRequested;
        }
    }

    private void SetSignOutRequested(bool value)
    {
        lock (_stateLock)
        {
            _signOutRequested = value;
        }
    }

    private async Task InvalidateLocalSessionAsync(
        bool removeCredential,
        bool revokeOfflineCapability = false)
    {
        ResetRefreshBackoff();
        if (revokeOfflineCapability)
        {
            await _offlineCapability.RevokeAsync(CancellationToken.None);
        }

        var offlineWasActive = _offlineSession.Lock();
        SetSession(null, offlineWasActive);
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

    private void SetSession(AuthenticationSessionModel? session, bool forceChanged = false)
    {
        bool changed;
        lock (_stateLock)
        {
            changed = forceChanged || (_currentSession is null) != (session is null) ||
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
            OidcFailureKindEnum.Rejected => AuthenticationResultEnum.Invalid,
            _ => AuthenticationResultEnum.TemporarilyUnavailable
        };
    }

    private sealed record OfflineRecoveryContext(
        OfflineCapabilityModel Capability,
        AccountSessionLeaseModel Lease);
}
