using FishingLogBook.Mobile.Features.Authentication.Models;

namespace FishingLogBook.Mobile.Features.Authentication.Coordinators;

public sealed class AccountSessionCoordinator : IAccountSessionCoordinator, IDisposable
{
    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _commitGate = new(1, 1);
    private CancellationTokenSource? _sessionCancellation;
    private Guid? _userId;
    private long _generation;
    private bool _transitionPending;

    public AccountSessionLeaseModel? Capture()
    {
        lock (_stateLock)
        {
            if (_userId is null || _sessionCancellation is null || _transitionPending)
            {
                return null;
            }

            return new AccountSessionLeaseModel(_userId.Value, _generation, _sessionCancellation.Token);
        }
    }

    public bool IsCurrent(AccountSessionLeaseModel lease)
    {
        lock (_stateLock)
        {
            return IsCurrentLocked(lease);
        }
    }

    public async Task TransitionAsync(Guid? userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CancellationTokenSource? previousCancellation;
        lock (_stateLock)
        {
            _transitionPending = true;
            previousCancellation = _sessionCancellation;
            previousCancellation?.Cancel();
        }

        await _commitGate.WaitAsync(CancellationToken.None);
        try
        {
            lock (_stateLock)
            {
                _generation++;
                _userId = userId;
                _sessionCancellation = userId.HasValue ? new CancellationTokenSource() : null;
                _transitionPending = false;
            }
        }
        finally
        {
            _commitGate.Release();
            previousCancellation?.Dispose();
        }
    }

    public async Task<bool> TryCommitAsync(
        AccountSessionLeaseModel lease,
        Func<CancellationToken, Task> commit,
        CancellationToken cancellationToken)
    {
        await _commitGate.WaitAsync(cancellationToken);
        try
        {
            if (!IsCurrent(lease))
            {
                return false;
            }

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lease.CancellationToken);
            await commit(linkedCancellation.Token);
            return IsCurrent(lease);
        }
        finally
        {
            _commitGate.Release();
        }
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            _sessionCancellation?.Cancel();
            _sessionCancellation?.Dispose();
            _sessionCancellation = null;
            _userId = null;
        }

        _commitGate.Dispose();
    }

    private bool IsCurrentLocked(AccountSessionLeaseModel lease)
    {
        return !_transitionPending &&
            _userId == lease.UserId &&
            _generation == lease.Generation &&
            _sessionCancellation is not null &&
            !_sessionCancellation.IsCancellationRequested;
    }
}
