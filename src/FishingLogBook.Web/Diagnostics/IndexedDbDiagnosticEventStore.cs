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

    public Task EnqueueAsync(DiagnosticEvent diagnosticEvent, CancellationToken cancellationToken)
    {
        return WithTimeoutAsync(
            (module, token) => module.InvokeVoidAsync(
                "putDiagnosticEvent",
                token,
                JsonSerializer.Serialize(diagnosticEvent, JsonOptions),
                _config.MaxQueueSize).AsTask(),
            cancellationToken);
    }

    public async Task<IReadOnlyList<DiagnosticEvent>> GetPendingAsync(int maxCount, CancellationToken cancellationToken)
    {
        var items = await WithTimeoutAsync(
            (module, token) => module.InvokeAsync<string[]>("getPendingDiagnosticEvents", token, maxCount).AsTask(),
            cancellationToken);
        return (items ?? [])
            .Select(json => JsonSerializer.Deserialize<DiagnosticEvent>(json, JsonOptions))
            .Where(item => item is not null)
            .Cast<DiagnosticEvent>()
            .ToArray();
    }

    public Task RemoveAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        return WithTimeoutAsync(
            (module, token) => module.InvokeVoidAsync(
                "deleteDiagnosticEvents",
                token,
                JsonSerializer.Serialize(ids.Select(id => id.ToString("D")).ToArray())).AsTask(),
            cancellationToken);
    }

    public Task<int> GetCountAsync(CancellationToken cancellationToken)
    {
        return WithTimeoutAsync(
            (module, token) => module.InvokeAsync<int>("getDiagnosticQueueCount", token).AsTask(),
            cancellationToken);
    }

    public Task SaveAsync(DiagnosticEvent diagnosticEvent, CancellationToken cancellationToken)
    {
        return EnqueueAsync(diagnosticEvent, cancellationToken);
    }

    public async Task<StorageEstimate> GetStorageEstimateAsync(CancellationToken cancellationToken)
    {
        var estimate = await WithTimeoutAsync(
            (module, token) => module.InvokeAsync<StorageEstimate?>("getStorageEstimate", token).AsTask(),
            cancellationToken);
        return estimate ?? new StorageEstimate();
    }

    private async Task WithTimeoutAsync(
        Func<IJSObjectReference, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        await WithTimeoutAsync(async (module, token) =>
        {
            await action(module, token);
            return 0;
        }, cancellationToken);
    }

    private async Task<T> WithTimeoutAsync<T>(
        Func<IJSObjectReference, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_config.OperationTimeout);
        var module = await GetModuleAsync(timeoutSource.Token);
        return await action(module, timeoutSource.Token);
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
            return _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
                "import",
                cancellationToken,
                ModulePath);
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
