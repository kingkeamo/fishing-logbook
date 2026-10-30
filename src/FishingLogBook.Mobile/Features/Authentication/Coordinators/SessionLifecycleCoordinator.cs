using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Services;
using FishingLogBook.Mobile.Features.Profile.Services;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Authentication.Coordinators;

public sealed class SessionLifecycleCoordinator : ISessionLifecycleCoordinator, IDisposable
{
    private static readonly TimeSpan RefreshWindow = TimeSpan.FromMinutes(1);
    private readonly object _operationLock = new();
    private readonly IConnectivityService _connectivity;
    private readonly IAuthenticationService _authentication;
    private readonly IOfflineSessionService _offlineSession;
    private readonly IAccountSessionCoordinator _accountSession;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _recoveryTimeout;
    private readonly ILogger<SessionLifecycleCoordinator> _logger;
    private CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _activeCancellation;
    private Task _activeOperation = Task.CompletedTask;
    private SessionLifecycleStateEnum _state;
    private bool _started;
    private bool _disposed;

    public SessionLifecycleCoordinator(
        IConnectivityService connectivity,
        IAuthenticationService authentication,
        IOfflineSessionService offlineSession,
        IAccountSessionCoordinator accountSession,
        TimeProvider timeProvider,
        TimeSpan recoveryTimeout,
        ILogger<SessionLifecycleCoordinator> logger)
    {
        _connectivity = connectivity;
        _authentication = authentication;
        _offlineSession = offlineSession;
        _accountSession = accountSession;
        _timeProvider = timeProvider;
        if (recoveryTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(recoveryTimeout));
        }

        _recoveryTimeout = recoveryTimeout;
        _logger = logger;
    }

    public event EventHandler? StateChanged;

    public SessionLifecycleStateEnum State
    {
        get
        {
            lock (_operationLock)
            {
                return _state;
            }
        }
    }

    public void Start()
    {
        lock (_operationLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_started)
            {
                return;
            }

            _started = true;
            _connectivity.ConnectivityChanged += OnConnectivityChanged;
        }
    }

    public Task ForegroundAsync(CancellationToken cancellationToken)
    {
        return RunOrRestartCancelledAsync(cancellationToken);
    }

    public Task RetryAsync(CancellationToken cancellationToken)
    {
        return RunOrRestartCancelledAsync(cancellationToken);
    }

    public void Background()
    {
        CancelActiveOperation();
    }

    public void Dispose()
    {
        CancellationTokenSource lifetime;
        CancellationTokenSource? active;
        lock (_operationLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _connectivity.ConnectivityChanged -= OnConnectivityChanged;
            lifetime = _lifetime;
            active = _activeCancellation;
            _activeCancellation = null;
        }

        lifetime.Cancel();
        active?.Cancel();
        active?.Dispose();
        lifetime.Dispose();
    }

    private Task RunAsync(CancellationToken cancellationToken)
    {
        lock (_operationLock)
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            if (!_activeOperation.IsCompleted)
            {
                return _activeOperation;
            }

            _activeCancellation?.Dispose();
            _activeCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetime.Token);
            _activeOperation = RecoverAsync(_activeCancellation.Token);
            return _activeOperation;
        }
    }

    private Task RunOrRestartCancelledAsync(CancellationToken cancellationToken)
    {
        Task active;
        lock (_operationLock)
        {
            if (_activeOperation.IsCompleted || _activeCancellation?.IsCancellationRequested != true)
            {
                return RunAsync(cancellationToken);
            }

            active = _activeOperation;
        }

        return RestartAfterCancellationAsync(active, cancellationToken);
    }

    private async Task RestartAfterCancellationAsync(Task active, CancellationToken cancellationToken)
    {
        try
        {
            await active;
        }
        catch (OperationCanceledException)
        {
        }

        await RunAsync(cancellationToken);
    }

    private async Task RecoverAsync(CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(_recoveryTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            if (!_connectivity.IsOnline)
            {
                SetState(HasLocalSession()
                    ? SessionLifecycleStateEnum.LocalOffline
                    : SessionLifecycleStateEnum.Idle);
                return;
            }

            var offlineCapability = _offlineSession.Current;
            if (offlineCapability is not null)
            {
                await RecoverOfflineSessionAsync(offlineCapability, linked.Token);
                return;
            }

            await ValidateOnlineSessionAsync(linked.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetState(HasLocalSession()
                ? SessionLifecycleStateEnum.LocalOffline
                : SessionLifecycleStateEnum.Idle);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            SetState(SessionLifecycleStateEnum.RecoverableError);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Mobile session lifecycle recovery failed.");
            SetState(SessionLifecycleStateEnum.RecoverableError);
        }
    }

    private async Task RecoverOfflineSessionAsync(
        OfflineCapabilityModel capability,
        CancellationToken cancellationToken)
    {
        var lease = _accountSession.Capture();
        if (!IsCurrent(lease, capability))
        {
            SetState(SessionLifecycleStateEnum.SignInRequired);
            return;
        }

        SetState(SessionLifecycleStateEnum.Recovering);
        var result = await _authentication.InitializeAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        ApplyRecoveryResult(result, capability, lease!);
    }

    private async Task ValidateOnlineSessionAsync(CancellationToken cancellationToken)
    {
        var session = _authentication.CurrentSession;
        if (session is null)
        {
            SetState(SessionLifecycleStateEnum.Idle);
            return;
        }

        if (session.AccessTokenExpiresAt > _timeProvider.GetUtcNow().Add(RefreshWindow))
        {
            SetState(SessionLifecycleStateEnum.Online);
            return;
        }

        var lease = _accountSession.Capture();
        if (!IsCurrent(lease, session))
        {
            SetState(SessionLifecycleStateEnum.SignInRequired);
            return;
        }

        SetState(SessionLifecycleStateEnum.Recovering);
        var result = await _authentication.InitializeAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (result == AuthenticationResultEnum.Success && IsCurrent(lease, session))
        {
            SetState(SessionLifecycleStateEnum.Online);
            return;
        }

        SetState(RequiresSignIn(result)
            ? SessionLifecycleStateEnum.SignInRequired
            : SessionLifecycleStateEnum.RecoverableError);
    }

    private void ApplyRecoveryResult(
        AuthenticationResultEnum result,
        OfflineCapabilityModel capability,
        AccountSessionLeaseModel lease)
    {
        if (!_connectivity.IsOnline)
        {
            SetState(_offlineSession.Current == capability
                ? SessionLifecycleStateEnum.LocalOffline
                : SessionLifecycleStateEnum.RecoverableError);
            return;
        }

        var session = _authentication.CurrentSession;
        if (result == AuthenticationResultEnum.Success &&
            session is not null &&
            IsCurrent(lease, session) &&
            string.Equals(session.Subject, capability.Subject, StringComparison.Ordinal) &&
            session.UserId == capability.UserId &&
            _offlineSession.Current is null)
        {
            SetState(SessionLifecycleStateEnum.Online);
            return;
        }

        SetState(RequiresSignIn(result)
            ? SessionLifecycleStateEnum.SignInRequired
            : SessionLifecycleStateEnum.RecoverableError);
    }

    private bool IsCurrent(AccountSessionLeaseModel? lease, OfflineCapabilityModel capability)
    {
        return lease is not null &&
            lease.UserId == capability.UserId &&
            _accountSession.IsCurrent(lease);
    }

    private bool IsCurrent(AccountSessionLeaseModel? lease, AuthenticationSessionModel session)
    {
        var current = _authentication.CurrentSession;
        return lease is not null &&
            _accountSession.IsCurrent(lease) &&
            current is not null &&
            current.UserId == session.UserId &&
            string.Equals(current.Subject, session.Subject, StringComparison.Ordinal);
    }

    private static bool RequiresSignIn(AuthenticationResultEnum result)
    {
        return result is AuthenticationResultEnum.Invalid or AuthenticationResultEnum.Pending;
    }

    private void OnConnectivityChanged(object? sender, EventArgs args)
    {
        if (!_connectivity.IsOnline)
        {
            CancelActiveOperation();
            SetState(HasLocalSession()
                ? SessionLifecycleStateEnum.LocalOffline
                : SessionLifecycleStateEnum.Idle);
            return;
        }

        Observe(RunOrRestartCancelledAsync(CancellationToken.None));
    }

    private void CancelActiveOperation()
    {
        lock (_operationLock)
        {
            _activeCancellation?.Cancel();
        }
    }

    private bool HasLocalSession()
    {
        return _offlineSession.Current is not null || _authentication.CurrentSession is not null;
    }

    private void SetState(SessionLifecycleStateEnum state)
    {
        lock (_operationLock)
        {
            if (_disposed || _state == state)
            {
                return;
            }

            _state = state;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void Observe(Task operation)
    {
        try
        {
            await operation;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Mobile lifecycle event could not be processed.");
        }
    }
}
