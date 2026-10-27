using FishingLogBook.Mobile.Features.Authentication.Models;

namespace FishingLogBook.Mobile.Features.Authentication.Coordinators;

public sealed class AccountSessionCoordinator : IAccountSessionCoordinator, IDisposable
{
    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _commitGate = new(1, 1);
    private CancellationTokenSource? _sessionCancellation;
    private Guid? _userId;
    private long _generation;
    private long _requestedTransition;
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
        long transition;
        lock (_stateLock)
        {
            if (IsActiveOwnerLocked(userId))
            {
                return;
            }

            transition = ++_requestedTransition;
            _transitionPending = true;
            _sessionCancellation?.Cancel();
        }

        await _commitGate.WaitAsync(CancellationToken.None);
        try
        {
            ApplyTransition(transition, userId);
        }
        finally
        {
            _commitGate.Release();
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

    private void ApplyTransition(long transition, Guid? userId)
    {
        CancellationTokenSource? previousCancellation;
        lock (_stateLock)
        {
            if (transition != _requestedTransition)
            {
                return;
            }

            previousCancellation = _sessionCancellation;
            _generation++;
            _userId = userId;
            _sessionCancellation = userId.HasValue ? new CancellationTokenSource() : null;
            _transitionPending = false;
        }

        previousCancellation?.Cancel();
        previousCancellation?.Dispose();
    }

    private bool IsActiveOwnerLocked(Guid? userId)
    {
        return userId.HasValue &&
            _userId == userId &&
            !_transitionPending &&
            _sessionCancellation is { IsCancellationRequested: false };
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
