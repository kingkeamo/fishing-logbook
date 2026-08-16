using AwesomeAssertions;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Diagnostics;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Tests.DiagnosticStoreTests;

public class WhenTestingTimeout
{
    [Fact]
    public async Task ItShouldTimeOut_WhenModuleImportNeverCompletes()
    {
        // Arrange
        var js = new HangingImportJsRuntime();
        var sut = new IndexedDbDiagnosticEventStore(
            js,
            new DiagnosticsClientConfig { OperationTimeoutMilliseconds = 250 });

        // Act
        var act = async () => await sut.GetCountAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class HangingImportJsRuntime : IJSRuntime
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
}
