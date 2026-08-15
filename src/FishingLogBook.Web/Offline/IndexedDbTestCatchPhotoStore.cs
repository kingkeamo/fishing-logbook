using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Diagnostics;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Offline;

public sealed class IndexedDbTestCatchPhotoStore : ITestCatchPhotoStore
{
    private const string ModulePath = "./js/offline-store.js";
    private const string StoreName = "testCatchPhotographs";

    private readonly IJSRuntime _jsRuntime;
    private readonly IDiagnosticLogger _diagnostics;
    private readonly ILoggingService _logging;
    private readonly DiagnosticsClientConfig _config;
    private readonly SemaphoreSlim _moduleLock = new(1, 1);
    private IJSObjectReference? _module;

    public IndexedDbTestCatchPhotoStore(
        IJSRuntime jsRuntime,
        IDiagnosticLogger diagnostics,
        ILoggingService logging,
        DiagnosticsClientConfig config)
    {
        _jsRuntime = jsRuntime;
        _diagnostics = diagnostics;
        _logging = logging;
        _config = config;
    }

    public async Task PutAsync(Guid testCatchId, byte[] bytes, string contentType, CancellationToken cancellationToken)
    {
        await OfflineOperation.ExecuteAsync(
            "write",
            StoreName,
            DiagnosticEventNames.PhotoOfflineSaveStarted,
            DiagnosticEventNames.PhotoOfflineSaveCompleted,
            DiagnosticEventNames.PhotoOfflineSaveFailed,
            DiagnosticEventNames.OfflineDbWriteTimedOut,
            _config.OperationTimeout,
            _diagnostics,
            async token =>
            {
                var module = await GetModuleAsync(token);
                await module.InvokeVoidAsync("putTestCatchPhotograph", token, testCatchId.ToString(), bytes, contentType);
            },
            cancellationToken,
            _logging);
    }

    public async Task<TestCatchPhotoBytes?> GetAsync(Guid testCatchId, CancellationToken cancellationToken)
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
                var stored = await module.InvokeAsync<PhotographJsDto?>(
                    "getTestCatchPhotograph",
                    token,
                    testCatchId.ToString());

                if (string.IsNullOrWhiteSpace(stored?.BytesBase64) || string.IsNullOrWhiteSpace(stored.ContentType))
                {
                    return null;
                }

                return new TestCatchPhotoBytes(Convert.FromBase64String(stored.BytesBase64), stored.ContentType);
            },
            cancellationToken,
            _logging);
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
