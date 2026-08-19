using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage;

namespace FishingLogBook.Web.Tests.Features.Diagnostics.Services.OfflineOperationTests;

public class WhenTestingTimeout : BaseOfflineOperationTest
{
    [Fact]
    public async Task ItShouldEmitATimeoutDiagnostic_WhenTheOperationExceedsTheTimeout()
    {
        // Arrange
        var logger = new RecordingDiagnosticLogger();

        // Act
        var act = async () => await OfflineOperation.ExecuteAsync(
            "read",
            "testCatches",
            DiagnosticEventNames.OfflineDbReadStarted,
            DiagnosticEventNames.OfflineDbReadCompleted,
            DiagnosticEventNames.OfflineDbReadFailed,
            DiagnosticEventNames.OfflineDbReadTimedOut,
            TimeSpan.FromMilliseconds(20),
            logger,
            async token =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2), token);
            },
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
        await WaitForEventAsync(logger, DiagnosticEventNames.OfflineDbReadTimedOut);
        await WaitForEventAsync(logger, DiagnosticEventNames.OfflineDbReadStarted);
    }

    [Fact]
    public async Task ItShouldNotTreatTheOperationAsCompleteBeforeTheActionFinishes()
    {
        // Arrange
        var logger = new RecordingDiagnosticLogger();
        var completed = false;

        // Act
        await OfflineOperation.ExecuteAsync(
            "write",
            "testCatches",
            DiagnosticEventNames.OfflineDbWriteStarted,
            DiagnosticEventNames.OfflineDbWriteCompleted,
            DiagnosticEventNames.OfflineDbWriteFailed,
            DiagnosticEventNames.OfflineDbWriteTimedOut,
            TimeSpan.FromSeconds(2),
            logger,
            async _ =>
            {
                await Task.Delay(30);
                completed = true;
            },
            CancellationToken.None);

        // Assert
        completed.Should().BeTrue();
        await WaitForEventAsync(logger, DiagnosticEventNames.OfflineDbWriteCompleted);
        await WaitForEventAsync(logger, DiagnosticEventNames.OfflineDbWriteStarted);
        var completedIndex = logger.Events.FindIndex(item => item.EventName == DiagnosticEventNames.OfflineDbWriteCompleted);
        completedIndex.Should().BeGreaterThan(0);
        logger.Events[completedIndex - 1].EventName.Should().Be(DiagnosticEventNames.OfflineDbWriteStarted);
    }

    [Fact]
    public async Task ItShouldCompleteTheAction_WhenDiagnosticLoggingNeverCompletes()
    {
        // Arrange
        var logger = new HangingDiagnosticLogger();
        var completed = false;

        // Act
        await OfflineOperation.ExecuteAsync(
            "write",
            "testCatches",
            DiagnosticEventNames.OfflineDbWriteStarted,
            DiagnosticEventNames.OfflineDbWriteCompleted,
            DiagnosticEventNames.OfflineDbWriteFailed,
            DiagnosticEventNames.OfflineDbWriteTimedOut,
            TimeSpan.FromSeconds(2),
            logger,
            _ =>
            {
                completed = true;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        // Assert
        completed.Should().BeTrue();
    }


    private sealed class HangingDiagnosticLogger : IDiagnosticLogger
    {
        public Task LogAsync(
            DiagnosticLevel level,
            string eventName,
            string message,
            IReadOnlyDictionary<string, string>? metadata = null,
            Exception? exception = null,
            CancellationToken cancellationToken = default)
        {
            return new TaskCompletionSource().Task;
        }
    }
}
