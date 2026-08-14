using System.Text.Json;
using FishingLogBook.Web.Configuration;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Diagnostics;

public sealed class IndexedDbDiagnosticEventStore : IDiagnosticEventStore
{
    private const string ModulePath = "./js/diagnostic-store.js";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IJSRuntime _jsRuntime;
    private readonly DiagnosticsClientConfig _config;
    private readonly SemaphoreSlim _moduleLock = new(1, 1);
    private IJSObjectReference? _module;

    public IndexedDbDiagnosticEventStore(IJSRuntime jsRuntime, DiagnosticsClientConfig config)
    {
        _jsRuntime = jsRuntime;
        _config = config;
    }

    public async Task EnqueueAsync(DiagnosticEvent diagnosticEvent, CancellationToken cancellationToken)
    {
        var module = await GetModuleAsync(cancellationToken);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_config.OperationTimeout);
        await module.InvokeVoidAsync(
            "putDiagnosticEvent",
            timeoutSource.Token,
            JsonSerializer.Serialize(diagnosticEvent, JsonOptions),
            _config.MaxQueueSize);
    }

    public async Task<IReadOnlyList<DiagnosticEvent>> GetPendingAsync(int maxCount, CancellationToken cancellationToken)
    {
        var module = await GetModuleAsync(cancellationToken);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_config.OperationTimeout);
        var items = await module.InvokeAsync<string[]>(
            "getPendingDiagnosticEvents",
            timeoutSource.Token,
            maxCount);
        return (items ?? [])
            .Select(json => JsonSerializer.Deserialize<DiagnosticEvent>(json, JsonOptions))
            .Where(item => item is not null)
            .Cast<DiagnosticEvent>()
            .ToArray();
    }

    public async Task RemoveAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var module = await GetModuleAsync(cancellationToken);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_config.OperationTimeout);
        await module.InvokeVoidAsync(
            "deleteDiagnosticEvents",
            timeoutSource.Token,
            ids.Select(id => id.ToString("D")).ToArray());
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken)
    {
        var module = await GetModuleAsync(cancellationToken);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_config.OperationTimeout);
        return await module.InvokeAsync<int>("getDiagnosticQueueCount", timeoutSource.Token);
    }

    public async Task SaveAsync(DiagnosticEvent diagnosticEvent, CancellationToken cancellationToken)
    {
        await EnqueueAsync(diagnosticEvent, cancellationToken);
    }

    public async Task<StorageEstimate> GetStorageEstimateAsync(CancellationToken cancellationToken)
    {
        var module = await GetModuleAsync(cancellationToken);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_config.OperationTimeout);
        var estimate = await module.InvokeAsync<StorageEstimate?>("getStorageEstimate", timeoutSource.Token);
        return estimate ?? new StorageEstimate();
    }

    private async Task<IJSObjectReference> GetModuleAsync(CancellationToken cancellationToken)
    {
        if (_module is not null)
        {
            return _module;
        }

        await _moduleLock.WaitAsync(cancellationToken);
        try
        {
            _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
            return _module;
        }
        finally
        {
            _moduleLock.Release();
        }
    }
}

public sealed class StorageEstimate
{
    public long? Quota { get; set; }

    public long? Usage { get; set; }
}
