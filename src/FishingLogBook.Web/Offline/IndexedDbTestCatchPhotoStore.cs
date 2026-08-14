using Microsoft.JSInterop;

namespace FishingLogBook.Web.Offline;

public sealed class IndexedDbTestCatchPhotoStore : ITestCatchPhotoStore
{
    private const string ModulePath = "./js/offline-store.js";

    private readonly IJSRuntime _jsRuntime;
    private readonly SemaphoreSlim _moduleLock = new(1, 1);
    private IJSObjectReference? _module;

    public IndexedDbTestCatchPhotoStore(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task PutAsync(Guid testCatchId, byte[] bytes, string contentType, CancellationToken cancellationToken)
    {
        var module = await GetModuleAsync(cancellationToken);
        await module.InvokeVoidAsync("putTestCatchPhotograph", cancellationToken, testCatchId.ToString(), bytes, contentType);
    }

    public async Task<TestCatchPhotoBytes?> GetAsync(Guid testCatchId, CancellationToken cancellationToken)
    {
        var module = await GetModuleAsync(cancellationToken);
        var stored = await module.InvokeAsync<PhotographJsDto?>(
            "getTestCatchPhotograph",
            cancellationToken,
            testCatchId.ToString());

        if (string.IsNullOrWhiteSpace(stored?.BytesBase64) || string.IsNullOrWhiteSpace(stored.ContentType))
        {
            return null;
        }

        return new TestCatchPhotoBytes(Convert.FromBase64String(stored.BytesBase64), stored.ContentType);
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

    private sealed class PhotographJsDto
    {
        public string? BytesBase64 { get; set; }

        public string? ContentType { get; set; }
    }
}
