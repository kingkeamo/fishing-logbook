namespace FishingLogBook.Mobile.Features.Catch.Services;

public sealed class ActiveCatchEditRegistry : IActiveCatchEditRegistry
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, int> _active = new();

    public IDisposable Acquire(Guid catchId)
    {
        lock (_lock)
        {
            _active[catchId] = _active.GetValueOrDefault(catchId) + 1;
        }

        return new Lease(this, catchId);
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
        lock (_lock)
        {
            if (!_active.TryGetValue(catchId, out var count))
            {
                return;
            }

            if (count <= 1)
            {
                _active.Remove(catchId);
                return;
            }

            _active[catchId] = count - 1;
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
