using FishingLogBook.Web.Diagnostics;
using FishingLogBook.Web.Models;

namespace FishingLogBook.Web.Tests.DiagnosticLoggerTests;

public sealed class MemoryDiagnosticEventStore : IDiagnosticEventStore
{
    public List<DiagnosticEvent> Items { get; } = [];

    public int MaxQueueSize { get; set; } = 500;

    public int EnqueueCalls { get; private set; }

    public Exception? ThrowOnEnqueue { get; set; }

    public Task EnqueueAsync(DiagnosticEvent diagnosticEvent, CancellationToken cancellationToken)
    {
        EnqueueCalls++;
        if (ThrowOnEnqueue is not null)
        {
            throw ThrowOnEnqueue;
        }

        var index = Items.FindIndex(item => item.Id == diagnosticEvent.Id);
        if (index >= 0)
        {
            Items[index] = diagnosticEvent;
        }
        else
        {
            Items.Add(diagnosticEvent);
        }

        DiagnosticQueue.TrimOldest(Items, MaxQueueSize);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DiagnosticEvent>> GetPendingAsync(int maxCount, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<DiagnosticEvent>>(Items.Take(maxCount).ToArray());
    }

    public Task RemoveAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        Items.RemoveAll(item => ids.Contains(item.Id));
        return Task.CompletedTask;
    }

    public Task<int> GetCountAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Items.Count);
    }

    public Task SaveAsync(DiagnosticEvent diagnosticEvent, CancellationToken cancellationToken)
    {
        return EnqueueAsync(diagnosticEvent, cancellationToken);
    }

    public Task<StorageEstimate> GetStorageEstimateAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new StorageEstimate { Quota = 1000, Usage = 10 });
    }
}
