using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage;
using FishingLogBook.Web.Features.Diagnostics.Storage.Stores;

namespace FishingLogBook.Web.Tests.Features.Diagnostics.TestSupport;

public sealed class MemoryDiagnosticEventStore : IDiagnosticEventStore
{
    public List<DiagnosticEventModel> Items { get; } = [];

    public int MaxQueueSize { get; set; } = 500;

    public int EnqueueCalls { get; private set; }

    public Exception? ThrowOnEnqueue { get; set; }

    public TaskCompletionSource<bool>? HangOnEnqueue { get; set; }

    public Task EnqueueAsync(DiagnosticEventModel diagnosticEvent, CancellationToken cancellationToken)
    {
        EnqueueCalls++;
        if (ThrowOnEnqueue is not null)
        {
            throw ThrowOnEnqueue;
        }

        if (HangOnEnqueue is not null)
        {
            return HangOnEnqueue.Task;
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

    public Task<IReadOnlyList<DiagnosticEventModel>> GetPendingAsync(int maxCount, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<DiagnosticEventModel>>(Items.Take(maxCount).ToArray());
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

    public Task<DiagnosticDatabaseInspectionModel> InspectExistingAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new DiagnosticDatabaseInspectionModel
        {
            Exists = true,
            HasStore = true,
            Count = Items.Count
        });
    }

    public Task SaveAsync(DiagnosticEventModel diagnosticEvent, CancellationToken cancellationToken)
    {
        return EnqueueAsync(diagnosticEvent, cancellationToken);
    }

    public Task<StorageEstimate> GetStorageEstimateAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new StorageEstimate { Quota = 1000, Usage = 10 });
    }
}
