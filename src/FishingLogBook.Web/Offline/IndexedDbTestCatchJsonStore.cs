using Microsoft.JSInterop;

namespace FishingLogBook.Web.Offline;

public sealed class IndexedDbTestCatchJsonStore : ITestCatchJsonStore
{
    private const string ModulePath = "./js/offline-store.js";

    private readonly IJSRuntime _jsRuntime;
    private readonly SemaphoreSlim _moduleLock = new(1, 1);
    private IJSObjectReference? _module;

    public IndexedDbTestCatchJsonStore(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task PutAsync(string json, CancellationToken cancellationToken)
    {
        var module = await GetModuleAsync(cancellationToken);
        await module.InvokeVoidAsync("putTestCatch", cancellationToken, json);
    }

    public async Task<IReadOnlyList<string>> GetAllAsync(CancellationToken cancellationToken)
    {
        var module = await GetModuleAsync(cancellationToken);
        var items = await module.InvokeAsync<string[]>("getAllTestCatches", cancellationToken);
        return items ?? [];
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
