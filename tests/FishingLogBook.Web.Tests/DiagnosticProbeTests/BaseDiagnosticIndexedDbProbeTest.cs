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
        public List<string> ImportPaths { get; } = [];

        public List<string> Invocations { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "import")
            {
                ImportPaths.Add(args?[0] as string ?? string.Empty);
                return ValueTask.FromResult((TValue)(object)this);
            }

            Invocations.Add(identifier);
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
