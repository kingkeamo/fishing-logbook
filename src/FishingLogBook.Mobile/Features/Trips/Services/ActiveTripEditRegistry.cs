namespace FishingLogBook.Mobile.Features.Trips.Services;

public sealed class ActiveTripEditRegistry : IActiveTripEditRegistry
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, int> _active = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public event EventHandler? EditReleased;

    public async Task<IDisposable> AcquireAsync(Guid tripId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            lock (_lock)
            {
                _active[tripId] = _active.GetValueOrDefault(tripId) + 1;
            }

            return new Lease(this, tripId);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RunIfInactiveAsync(Guid? tripId, Func<Task<bool>> action, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            lock (_lock)
            {
                if (tripId is { } id ? _active.ContainsKey(id) : _active.Count > 0)
                {
                    return false;
                }
            }

            return await action();
        }
        finally
        {
            _gate.Release();
        }
    }

    public bool IsActive(Guid tripId)
    {
        lock (_lock)
        {
            return _active.ContainsKey(tripId);
        }
    }

    private void Release(Guid tripId)
    {
        var released = false;
        lock (_lock)
        {
            if (!_active.TryGetValue(tripId, out var count))
            {
                return;
            }

            if (count <= 1)
            {
                _active.Remove(tripId);
                released = true;
            }
            else
            {
                _active[tripId] = count - 1;
            }
        }

        if (released)
        {
            EditReleased?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class Lease(ActiveTripEditRegistry registry, Guid tripId) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                registry.Release(tripId);
            }
        }
    }
}
