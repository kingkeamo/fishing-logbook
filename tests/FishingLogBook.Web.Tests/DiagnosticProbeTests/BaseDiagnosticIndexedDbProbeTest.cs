using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Diagnostics;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Tests.DiagnosticProbeTests;

public class BaseDiagnosticIndexedDbProbeTest
{
    protected static BrowserDiagnosticIndexedDbProbe CreateSut(IJSRuntime jsRuntime, int timeoutMilliseconds = 1000)
    {
        return new BrowserDiagnosticIndexedDbProbe(
            jsRuntime,
            new DiagnosticsClientConfig { OperationTimeoutMilliseconds = timeoutMilliseconds });
    }

    protected sealed class HangingImportJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            return new ValueTask<TValue>(Hang<TValue>(cancellationToken));
        }

        private static async Task<TValue> Hang<TValue>(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return default!;
        }
    }

    protected sealed class RecordingProbeJsRuntime : IJSRuntime, IJSObjectReference
    {
        public List<string> DatabaseNames { get; } = [];

        public List<string> StoreNames { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "import")
            {
                return ValueTask.FromResult((TValue)(object)this);
            }

            if (args is { Length: > 0 } && args[0] is string databaseName)
            {
                DatabaseNames.Add(databaseName);
            }

            if (args is { Length: > 1 } && args[1] is string storeName)
            {
                StoreNames.Add(storeName);
            }

            if (identifier == "countProbeRecords")
            {
                return ValueTask.FromResult((TValue)(object)1);
            }

            return default!;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
