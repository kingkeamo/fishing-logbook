using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Common.Offline;
using FishingLogBook.Web.Features.Diagnostics.Services;

namespace FishingLogBook.Web.Tests.Common.Offline.OfflineOperationTests;

public class WhenTestingCancellation : BaseOfflineOperationTest
{
    [Fact]
    public async Task ItShouldNotEmitAFailureDiagnosticWhenTheCallerCancels()
    {
        // Arrange
        var logger = new RecordingDiagnosticLogger();
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource();

        // Act
        var operation = OfflineOperation.ExecuteAsync(
            "write",
            "productionCatches",
            DiagnosticEventNames.OfflineDbWriteStarted,
            DiagnosticEventNames.OfflineDbWriteCompleted,
            DiagnosticEventNames.OfflineDbWriteFailed,
            DiagnosticEventNames.OfflineDbWriteTimedOut,
            TimeSpan.FromSeconds(30),
            logger,
            async token =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
            },
            cancellation.Token);
        await started.Task;
        await cancellation.CancelAsync();
        var act = async () => await operation;

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        await WaitForEventAsync(logger, DiagnosticEventNames.OfflineDbWriteStarted);
        logger.Events.Should().NotContain(item =>
            item.EventName == DiagnosticEventNames.OfflineDbWriteFailed);
        logger.Events.Should().NotContain(item =>
            item.EventName == DiagnosticEventNames.OfflineDbWriteTimedOut);
    }

    [Fact]
    public async Task ItShouldStillEmitAFailureDiagnosticWhenTheOperationFailsWithoutCancellation()
    {
        // Arrange
        var logger = new RecordingDiagnosticLogger();
        using var cancellation = new CancellationTokenSource();

        // Act
        var act = async () => await OfflineOperation.ExecuteAsync(
            "write",
            "productionCatches",
            DiagnosticEventNames.OfflineDbWriteStarted,
            DiagnosticEventNames.OfflineDbWriteCompleted,
            DiagnosticEventNames.OfflineDbWriteFailed,
            DiagnosticEventNames.OfflineDbWriteTimedOut,
            TimeSpan.FromSeconds(30),
            logger,
            _ => throw new InvalidOperationException("boom"),
            cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await WaitForEventAsync(logger, DiagnosticEventNames.OfflineDbWriteFailed);
    }
}
