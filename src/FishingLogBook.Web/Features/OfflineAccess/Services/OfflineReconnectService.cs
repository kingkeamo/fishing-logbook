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
    private CancellationTokenSource? _monitoringCancellationTokenSource;
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

        try
        {
            await AttemptCoreAsync(cancellationToken);
        }
        finally
        {
            Interlocked.Exchange(ref _attempting, 0);
        }
    }

    public void Stop()
    {
        if (!_monitoring)
        {
            return;
        }

        _networkService.ConnectivityChanged -= OnConnectivityChanged;
        _monitoringCancellationTokenSource?.Cancel();
        _monitoringCancellationTokenSource?.Dispose();
        _monitoringCancellationTokenSource = null;
        Interlocked.Exchange(ref _automaticAttempted, 0);
        _monitoring = false;
    }

    private async Task AttemptCoreAsync(CancellationToken cancellationToken)
    {
        var offlineOwner = _offlineOwnerContext.Owner;
        if (offlineOwner is null)
        {
            SetState(OfflineReconnectStateEnum.Offline);
            return;
        }

        SetState(OfflineReconnectStateEnum.ConnectivityRestored);
        SetState(OfflineReconnectStateEnum.RecoveringAuthentication);

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
            await FailAsync("offline reconnect authentication", exception);
            return;
        }

        if (authentication.User.Identity?.IsAuthenticated != true)
        {
            SetState(OfflineReconnectStateEnum.AuthenticationRequired);
            return;
        }

        SetState(OfflineReconnectStateEnum.VerifyingOwner);
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
            SetState(OfflineReconnectStateEnum.AuthenticationRequired);
            return;
        }
        catch (Exception exception)
        {
            await FailAsync("offline reconnect owner verification", exception);
            return;
        }

        if (authenticatedUserId != offlineOwner.UserId)
        {
            SetState(OfflineReconnectStateEnum.OwnerMismatch);
            return;
        }

        if (!HasSameUnlockedOwner(offlineOwner.UserId))
        {
            SetState(OfflineReconnectStateEnum.Offline);
            return;
        }

        SetState(OfflineReconnectStateEnum.Synchronising);
        try
        {
            await _catchSynchroniser.SynchronisePendingAsync(authenticatedUserId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            await FailAsync("offline reconnect synchronisation", exception);
            return;
        }

        if (!HasSameUnlockedOwner(offlineOwner.UserId))
        {
            SetState(OfflineReconnectStateEnum.Offline);
            return;
        }

        _offlineOwnerContext.Lock();
        SetState(OfflineReconnectStateEnum.Online);
    }

    private void OnConnectivityChanged(bool isOnline)
    {
        if (isOnline)
        {
            var cancellationToken = _monitoringCancellationTokenSource?.Token ?? CancellationToken.None;
            _ = AttemptAutomaticallyAsync(cancellationToken);
            return;
        }

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
}
