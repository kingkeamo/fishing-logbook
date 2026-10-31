namespace FishingLogBook.Mobile.Features.Catch.Services;

public sealed class ActiveCatchEditRegistry : IActiveCatchEditRegistry
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, int> _active = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    public event EventHandler? EditReleased;

    public async Task<IDisposable> AcquireAsync(Guid catchId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            lock (_lock)
            {
                _active[catchId] = _active.GetValueOrDefault(catchId) + 1;
            }

            return new Lease(this, catchId);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RunIfInactiveAsync(Guid? catchId, Func<Task<bool>> action, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            lock (_lock)
            {
                if (catchId is { } id ? _active.ContainsKey(id) : _active.Count > 0)
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

    public bool IsActive(Guid catchId)
    {
        lock (_lock)
        {
            return _active.ContainsKey(catchId);
        }
    }

    private void Release(Guid catchId)
    {
        var released = false;
        lock (_lock)
        {
            if (!_active.TryGetValue(catchId, out var count))
            {
                return;
            }

            if (count <= 1)
            {
                _active.Remove(catchId);
                released = true;
            }
            else
            {
                _active[catchId] = count - 1;
            }
        }

        if (released)
        {
            EditReleased?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class Lease(ActiveCatchEditRegistry registry, Guid catchId) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                registry.Release(catchId);
            }
        }
    }
}
