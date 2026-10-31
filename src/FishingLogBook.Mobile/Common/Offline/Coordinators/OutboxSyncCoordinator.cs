using FishingLogBook.Mobile.Common.Offline.Constants;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Models;
using FishingLogBook.Mobile.Common.Offline.Services;
using FishingLogBook.Mobile.Common.Offline.Sqlite;
using FishingLogBook.Mobile.Common.Offline.Stores;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Common.Offline.Coordinators;

public sealed class OutboxSyncCoordinator : IOutboxSyncCoordinator, IDisposable
{
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MinimumTimerDelay = TimeSpan.FromSeconds(1);

    private readonly object _stateLock = new();
    private readonly string _owner = Guid.NewGuid().ToString("N");
    private readonly IAccountSessionCoordinator _session;
    private readonly IConnectivityService _connectivity;
    private readonly ISessionLifecycleCoordinator _lifecycle;
    private readonly IOutboxStore _outbox;
    private readonly ISqliteDatabase _database;
    private readonly IReadOnlyDictionary<OutboxOperationTypeEnum, IOutboxOperationSynchroniser> _synchronisers;
    private readonly IOutboxChangeNotifier _notifier;
    private readonly OutboxBackoffPolicy _backoff;
    private readonly TimeProvider _time;
    private readonly ILogger<OutboxSyncCoordinator> _logger;
    private CancellationTokenSource? _runCancellation;
    private ITimer? _retryTimer;
    private bool _running;
    private bool _rerunRequested;
    private bool _started;
    private bool _disposed;

    public OutboxSyncCoordinator(
        IAccountSessionCoordinator session,
        IConnectivityService connectivity,
        ISessionLifecycleCoordinator lifecycle,
        IOutboxStore outbox,
        ISqliteDatabase database,
        IEnumerable<IOutboxOperationSynchroniser> synchronisers,
        IOutboxChangeNotifier notifier,
        OutboxBackoffPolicy backoff,
        TimeProvider time,
        ILogger<OutboxSyncCoordinator> logger)
    {
        _session = session;
        _connectivity = connectivity;
        _lifecycle = lifecycle;
        _outbox = outbox;
        _database = database;
        _synchronisers = synchronisers.ToDictionary(synchroniser => synchroniser.OperationType);
        _notifier = notifier;
        _backoff = backoff;
        _time = time;
        _logger = logger;
    }

    public void Start()
    {
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_started)
            {
                return;
            }

            _started = true;
            _lifecycle.StateChanged += OnLifecycleStateChanged;
            _notifier.WorkEnqueued += OnWorkEnqueued;
        }
    }

    public void Request(OutboxSyncTriggerEnum trigger)
    {
        Observe(RunAsync(trigger, CancellationToken.None));
    }

    public async Task RunAsync(OutboxSyncTriggerEnum trigger, CancellationToken cancellationToken)
    {
        CancellationTokenSource runCancellation;
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            if (_running)
            {
                _rerunRequested = true;
                return;
            }

            _running = true;
            _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            runCancellation = _runCancellation;
        }

        _logger.LogDebug("Outbox synchronisation run started by {Trigger}.", trigger);
        await RunUntilIdleAsync(runCancellation);
    }

    public void Pause()
    {
        lock (_stateLock)
        {
            _runCancellation?.Cancel();
            DisposeTimerLocked();
        }
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifecycle.StateChanged -= OnLifecycleStateChanged;
            _notifier.WorkEnqueued -= OnWorkEnqueued;
            _runCancellation?.Cancel();
            DisposeTimerLocked();
        }
    }

    private async Task RunUntilIdleAsync(CancellationTokenSource runCancellation)
    {
        var again = true;
        while (again)
        {
            try
            {
                await DrainAsync(runCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Outbox synchronisation run was cancelled.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Outbox synchronisation run failed.");
            }

            lock (_stateLock)
            {
                again = _rerunRequested && !runCancellation.IsCancellationRequested && !_disposed;
                _rerunRequested = false;
                if (!again)
                {
                    _running = false;
                    _runCancellation = null;
                }
            }
        }

        runCancellation.Dispose();
    }

    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        var lease = _session.Capture();
        if (lease is null || !_connectivity.IsOnline)
        {
            return;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lease.CancellationToken);
        try
        {
            await _outbox.RecoverExpiredLeasesAsync(lease.UserId, _owner, _time.GetUtcNow(), linked.Token);
            while (_session.IsCurrent(lease) && _connectivity.IsOnline)
            {
                var claimed = await _outbox.ClaimNextAsync(
                    lease.UserId,
                    _owner,
                    _time.GetUtcNow(),
                    LeaseDuration,
                    linked.Token);
                if (claimed is null)
                {
                    break;
                }

                _notifier.NotifyStateChanged();
                var proceed = await ProcessAsync(claimed, lease, linked.Token);
                _notifier.NotifyStateChanged();
                if (!proceed)
                {
                    break;
                }
            }
        }
        finally
        {
            await ScheduleNextAttemptAsync(lease);
            _notifier.NotifyStateChanged();
        }
    }

    private async Task<bool> ProcessAsync(
        OutboxOperationModel claimed,
        AccountSessionLeaseModel lease,
        CancellationToken cancellationToken)
    {
        if (!_synchronisers.TryGetValue(claimed.OperationType, out var synchroniser))
        {
            await _outbox.MarkTerminalAsync(
                claimed,
                OutboxStatusEnum.Failed,
                OutboxErrorCodeConstants.NoSynchroniser,
                CancellationToken.None);
            return true;
        }

        OutboxOperationResultModel result;
        try
        {
            result = await synchroniser.SynchroniseAsync(claimed, lease, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await _outbox.ReleaseAsync(claimed, CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Outbox operation {OperationId} ({OperationType}) failed unexpectedly.",
                claimed.Id,
                claimed.OperationType);
            result = OutboxOperationResultModel.Retry(OutboxErrorCodeConstants.Unexpected);
        }

        return await ApplyAsync(claimed, result);
    }

    private async Task<bool> ApplyAsync(OutboxOperationModel claimed, OutboxOperationResultModel result)
    {
        switch (result.Outcome)
        {
            case OutboxOperationOutcomeEnum.Completed:
                await CompleteAsync(claimed, result);
                return true;
            case OutboxOperationOutcomeEnum.Retry:
                return await ScheduleRetryAsync(claimed, result.ErrorCode ?? OutboxErrorCodeConstants.Unexpected);
            case OutboxOperationOutcomeEnum.Terminal:
                await _outbox.MarkTerminalAsync(
                    claimed,
                    OutboxStatusEnum.Failed,
                    result.ErrorCode ?? OutboxErrorCodeConstants.Unexpected,
                    CancellationToken.None);
                return true;
            default:
                await _outbox.ReleaseAsync(claimed, CancellationToken.None);
                return false;
        }
    }

    private async Task CompleteAsync(OutboxOperationModel claimed, OutboxOperationResultModel result)
    {
        var now = _time.GetUtcNow();
        try
        {
            await _database.ExecuteInTransactionAsync(
                async (connection, transaction, token) =>
                {
                    if (result.Persist is not null)
                    {
                        await result.Persist(connection, transaction, token);
                    }

                    await _outbox.CompleteAsync(connection, transaction, claimed, now, token);
                },
                CancellationToken.None);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(
                exception,
                "Outbox operation {OperationId} could not be completed; its lease was lost or its result was rejected.",
                claimed.Id);
        }
    }

    private async Task<bool> ScheduleRetryAsync(OutboxOperationModel claimed, string errorCode)
    {
        if (!_connectivity.IsOnline)
        {
            await _outbox.ReleaseAsync(claimed, CancellationToken.None);
            return false;
        }

        if (_backoff.IsExhausted(claimed.AttemptCount))
        {
            await _outbox.MarkTerminalAsync(claimed, OutboxStatusEnum.Dead, errorCode, CancellationToken.None);
            return true;
        }

        var availableAt = _time.GetUtcNow().Add(_backoff.NextDelay(claimed.AttemptCount));
        await _outbox.ScheduleRetryAsync(claimed, availableAt, errorCode, CancellationToken.None);
        return true;
    }

    private async Task ScheduleNextAttemptAsync(AccountSessionLeaseModel lease)
    {
        if (!_session.IsCurrent(lease) || !_connectivity.IsOnline)
        {
            return;
        }

        var next = await _outbox.GetNextDueAsync(lease.UserId, CancellationToken.None);
        lock (_stateLock)
        {
            DisposeTimerLocked();
            if (next is null || _disposed)
            {
                return;
            }

            var delay = next.Value - _time.GetUtcNow();
            _retryTimer = _time.CreateTimer(
                _ => Request(OutboxSyncTriggerEnum.RetryDue),
                null,
                delay < MinimumTimerDelay ? MinimumTimerDelay : delay,
                Timeout.InfiniteTimeSpan);
        }
    }

    private void DisposeTimerLocked()
    {
        _retryTimer?.Dispose();
        _retryTimer = null;
    }

    private void OnLifecycleStateChanged(object? sender, EventArgs args)
    {
        if (_lifecycle.State == SessionLifecycleStateEnum.Online)
        {
            Request(OutboxSyncTriggerEnum.SessionOnline);
        }
        else
        {
            Pause();
        }
    }

    private void OnWorkEnqueued(object? sender, EventArgs args)
    {
        Request(OutboxSyncTriggerEnum.WorkEnqueued);
    }

    private async void Observe(Task operation)
    {
        try
        {
            await operation;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Outbox synchronisation trigger could not be processed.");
        }
    }
}
