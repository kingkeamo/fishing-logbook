using System.Net;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Features.Catch.Offline.Synchronisers;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.OfflineAccess.Enums;
using FishingLogBook.Web.Features.Users.Clients;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace FishingLogBook.Web.Features.OfflineAccess.Services;

public sealed class OfflineReconnectService : IOfflineReconnectService
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly ICurrentUserClient _currentUserClient;
    private readonly ICatchSynchroniser _catchSynchroniser;
    private readonly IOfflineOwnerContextService _offlineOwnerContext;
    private readonly ILoggingService _logging;
    private readonly INetworkService _networkService;
    private readonly object _attemptLock = new();
    private CancellationTokenSource? _activeAttemptCancellationTokenSource;
    private CancellationTokenSource? _monitoringCancellationTokenSource;
    private long _attemptGeneration;
    private int _automaticAttempted;
    private int _attempting;
    private bool _monitoring;

    public OfflineReconnectService(
        AuthenticationStateProvider authenticationStateProvider,
        ICurrentUserClient currentUserClient,
        ICatchSynchroniser catchSynchroniser,
        IOfflineOwnerContextService offlineOwnerContext,
        ILoggingService logging,
        INetworkService networkService)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _currentUserClient = currentUserClient;
        _catchSynchroniser = catchSynchroniser;
        _offlineOwnerContext = offlineOwnerContext;
        _logging = logging;
        _networkService = networkService;
    }

    public event EventHandler? StateChanged;

    public OfflineReconnectStateEnum State { get; private set; } = OfflineReconnectStateEnum.Offline;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_monitoring)
        {
            _monitoringCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _networkService.ConnectivityChanged += OnConnectivityChanged;
            _monitoring = true;
        }

        var monitoringToken = _monitoringCancellationTokenSource?.Token ?? cancellationToken;
        try
        {
            await _networkService.StartMonitoringAsync(monitoringToken);
            if (await _networkService.IsOnlineAsync(monitoringToken))
            {
                await AttemptAutomaticallyAsync(monitoringToken);
            }
        }
        catch (OperationCanceledException) when (monitoringToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            await FailAsync("offline reconnect connectivity", exception);
        }
    }

    public async Task AttemptAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _attempting, 1, 0) != 0)
        {
            return;
        }

        var attempt = BeginAttempt(cancellationToken);
        try
        {
            await AttemptCoreAsync(attempt.Generation, attempt.CancellationTokenSource.Token);
        }
        finally
        {
            CompleteAttempt(attempt);
            Interlocked.Exchange(ref _attempting, 0);
        }
    }

    public void Stop()
    {
        InvalidateActiveAttempt();
        if (_monitoring)
        {
            _networkService.ConnectivityChanged -= OnConnectivityChanged;
            _monitoringCancellationTokenSource?.Cancel();
            _monitoringCancellationTokenSource?.Dispose();
            _monitoringCancellationTokenSource = null;
            Interlocked.Exchange(ref _automaticAttempted, 0);
            _monitoring = false;
        }

        SetState(OfflineReconnectStateEnum.Offline);
    }

    private async Task AttemptCoreAsync(long generation, CancellationToken cancellationToken)
    {
        var offlineOwner = _offlineOwnerContext.Owner;
        if (offlineOwner is null)
        {
            SetState(OfflineReconnectStateEnum.Offline);
            return;
        }

        if (!TrySetAttemptState(generation, cancellationToken, OfflineReconnectStateEnum.ConnectivityRestored)
            || !TrySetAttemptState(generation, cancellationToken, OfflineReconnectStateEnum.RecoveringAuthentication))
        {
            return;
        }

        AuthenticationState authentication;
        try
        {
            authentication = await _authenticationStateProvider.GetAuthenticationStateAsync()
                .WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            await FailAttemptAsync(generation, cancellationToken, "offline reconnect authentication", exception);
            return;
        }

        if (!IsCurrentAttempt(generation, cancellationToken))
        {
            return;
        }

        if (authentication.User.Identity?.IsAuthenticated != true)
        {
            TrySetAttemptState(generation, cancellationToken, OfflineReconnectStateEnum.AuthenticationRequired);
            return;
        }

        if (!TrySetAttemptState(generation, cancellationToken, OfflineReconnectStateEnum.VerifyingOwner))
        {
            return;
        }

        Guid authenticatedUserId;
        try
        {
            var currentUser = await _currentUserClient.GetCurrentAsync(cancellationToken);
            authenticatedUserId = currentUser.UserId;
            if (authenticatedUserId == Guid.Empty)
            {
                throw new InvalidOperationException("The authenticated user could not be resolved.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception) when (IsAuthenticationRequired(exception))
        {
            TrySetAttemptState(generation, cancellationToken, OfflineReconnectStateEnum.AuthenticationRequired);
            return;
        }
        catch (Exception exception)
        {
            await FailAttemptAsync(generation, cancellationToken, "offline reconnect owner verification", exception);
            return;
        }

        if (!IsCurrentAttempt(generation, cancellationToken))
        {
            return;
        }

        if (authenticatedUserId != offlineOwner.UserId)
        {
            TrySetAttemptState(generation, cancellationToken, OfflineReconnectStateEnum.OwnerMismatch);
            return;
        }

        if (!HasSameUnlockedOwner(offlineOwner.UserId))
        {
            TrySetAttemptState(generation, cancellationToken, OfflineReconnectStateEnum.Offline);
            return;
        }

        try
        {
            if (!TryStartSynchronisation(
                    generation,
                    cancellationToken,
                    authenticatedUserId,
                    out var synchronisation))
            {
                return;
            }

            await synchronisation;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            await FailAttemptAsync(generation, cancellationToken, "offline reconnect synchronisation", exception);
            return;
        }

        TryCompleteOnline(generation, cancellationToken, offlineOwner.UserId);
    }

    private void OnConnectivityChanged(bool isOnline)
    {
        if (isOnline)
        {
            var cancellationToken = _monitoringCancellationTokenSource?.Token ?? CancellationToken.None;
            _ = AttemptAutomaticallyAsync(cancellationToken);
            return;
        }

        InvalidateActiveAttempt();
        Interlocked.Exchange(ref _automaticAttempted, 0);
        SetState(OfflineReconnectStateEnum.Offline);
    }

    private Task AttemptAutomaticallyAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _automaticAttempted, 1, 0) != 0)
        {
            return Task.CompletedTask;
        }

        return AttemptAsync(cancellationToken);
    }

    private bool HasSameUnlockedOwner(Guid userId)
    {
        return _offlineOwnerContext.Owner?.UserId == userId;
    }

    private AttemptLifecycle BeginAttempt(CancellationToken cancellationToken)
    {
        lock (_attemptLock)
        {
            var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activeAttemptCancellationTokenSource = cancellationTokenSource;
            _attemptGeneration++;
            return new AttemptLifecycle(_attemptGeneration, cancellationTokenSource);
        }
    }

    private void CompleteAttempt(AttemptLifecycle attempt)
    {
        lock (_attemptLock)
        {
            if (ReferenceEquals(_activeAttemptCancellationTokenSource, attempt.CancellationTokenSource))
            {
                _activeAttemptCancellationTokenSource = null;
            }
        }

        attempt.CancellationTokenSource.Dispose();
    }

    private void InvalidateActiveAttempt()
    {
        CancellationTokenSource? cancellationTokenSource;
        lock (_attemptLock)
        {
            _attemptGeneration++;
            cancellationTokenSource = _activeAttemptCancellationTokenSource;
            _activeAttemptCancellationTokenSource = null;
        }

        cancellationTokenSource?.Cancel();
    }

    private bool IsCurrentAttempt(long generation, CancellationToken cancellationToken)
    {
        lock (_attemptLock)
        {
            return !cancellationToken.IsCancellationRequested
                && generation == _attemptGeneration;
        }
    }

    private bool TrySetAttemptState(
        long generation,
        CancellationToken cancellationToken,
        OfflineReconnectStateEnum state)
    {
        if (!IsCurrentAttempt(generation, cancellationToken))
        {
            return false;
        }

        SetState(state);
        return true;
    }

    private bool TryCompleteOnline(long generation, CancellationToken cancellationToken, Guid ownerUserId)
    {
        lock (_attemptLock)
        {
            if (cancellationToken.IsCancellationRequested
                || generation != _attemptGeneration
                || !HasSameUnlockedOwner(ownerUserId))
            {
                return false;
            }

            _offlineOwnerContext.Lock();
            SetState(OfflineReconnectStateEnum.Online);
            _ = _catchSynchroniser.CleanupSyncedCacheAsync(ownerUserId, cancellationToken);
            return true;
        }
    }

    private bool TryStartSynchronisation(
        long generation,
        CancellationToken cancellationToken,
        Guid ownerUserId,
        out Task synchronisation)
    {
        lock (_attemptLock)
        {
            if (cancellationToken.IsCancellationRequested
                || generation != _attemptGeneration
                || !HasSameUnlockedOwner(ownerUserId))
            {
                synchronisation = Task.CompletedTask;
                return false;
            }

            SetState(OfflineReconnectStateEnum.Synchronising);
            synchronisation = _catchSynchroniser.SynchronisePendingAsync(ownerUserId, cancellationToken);
            return true;
        }
    }

    private async Task FailAttemptAsync(
        long generation,
        CancellationToken cancellationToken,
        string operation,
        Exception exception)
    {
        if (!IsCurrentAttempt(generation, cancellationToken))
        {
            return;
        }

        await _logging.LogErrorAsync(operation, exception, CancellationToken.None);
        TrySetAttemptState(generation, cancellationToken, OfflineReconnectStateEnum.RetryableFailure);
    }

    private async Task FailAsync(string operation, Exception exception)
    {
        await _logging.LogErrorAsync(operation, exception, CancellationToken.None);
        SetState(OfflineReconnectStateEnum.RetryableFailure);
    }

    private void SetState(OfflineReconnectStateEnum state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsAuthenticationRequired(Exception exception)
    {
        return exception is AccessTokenNotAvailableException
            || exception is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized };
    }

    private sealed record AttemptLifecycle(
        long Generation,
        CancellationTokenSource CancellationTokenSource);
}
