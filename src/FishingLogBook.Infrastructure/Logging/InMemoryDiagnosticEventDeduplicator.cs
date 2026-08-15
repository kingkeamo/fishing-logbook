using System.Collections.Concurrent;
using FishingLogBook.Application.Contracts;

namespace FishingLogBook.Infrastructure.Logging;

public sealed class InMemoryDiagnosticEventDeduplicator : IDiagnosticEventDeduplicator
{
    private const int Capacity = 2000;

    private readonly ConcurrentDictionary<Guid, byte> _seen = new();
    private readonly ConcurrentQueue<Guid> _order = new();

    public bool TryAccept(Guid id)
    {
        if (id == Guid.Empty || !_seen.TryAdd(id, 0))
        {
            return false;
        }

        _order.Enqueue(id);
        while (_seen.Count > Capacity && _order.TryDequeue(out var oldest))
        {
            _seen.TryRemove(oldest, out _);
        }

        return true;
    }
}
