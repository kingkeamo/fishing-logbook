using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Diagnostics;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Offline;

public sealed class IndexedDbTestCatchJsonStore : ITestCatchJsonStore
{
    private const string ModulePath = "./js/offline-store.js";
    private const string StoreName = "testCatches";

    private readonly IJSRuntime _jsRuntime;
    private readonly IDiagnosticLogger _diagnostics;
    private readonly DiagnosticsClientConfig _config;
    private readonly SemaphoreSlim _moduleLock = new(1, 1);
    private IJSObjectReference? _module;

    public IndexedDbTestCatchJsonStore(
        IJSRuntime jsRuntime,
        IDiagnosticLogger diagnostics,
        DiagnosticsClientConfig config)
    {
        _jsRuntime = jsRuntime;
        _diagnostics = diagnostics;
        _config = config;
    }

    public async Task PutAsync(string json, CancellationToken cancellationToken)
    {
        await OfflineOperation.ExecuteAsync(
            "write",
            StoreName,
            DiagnosticEventNames.OfflineDbWriteStarted,
            DiagnosticEventNames.OfflineDbWriteCompleted,
            DiagnosticEventNames.OfflineDbWriteFailed,
            DiagnosticEventNames.OfflineDbWriteTimedOut,
            _config.OperationTimeout,
            _diagnostics,
            async token =>
            {
                var module = await GetModuleAsync(token);
                await module.InvokeVoidAsync("putTestCatch", token, json);
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await OfflineOperation.ExecuteAsync(
            "read",
            StoreName,
            DiagnosticEventNames.OfflineDbReadStarted,
            DiagnosticEventNames.OfflineDbReadCompleted,
            DiagnosticEventNames.OfflineDbReadFailed,
            DiagnosticEventNames.OfflineDbReadTimedOut,
            _config.OperationTimeout,
            _diagnostics,
            async token =>
            {
                var module = await GetModuleAsync(token);
                var items = await module.InvokeAsync<string[]>("getAllTestCatches", token);
                return (IReadOnlyList<string>)(items ?? []);
            },
            cancellationToken);
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
