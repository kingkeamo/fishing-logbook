using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Diagnostics;

namespace FishingLogBook.Web.Tests.OfflineOperationTests;

public class WhenTestingTimeout
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
        logger.Events.Should().Contain(item => item.EventName == DiagnosticEventNames.OfflineDbReadTimedOut);
        logger.Events.Should().Contain(item => item.EventName == DiagnosticEventNames.OfflineDbReadStarted);
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
        var completedIndex = logger.Events.FindIndex(item => item.EventName == DiagnosticEventNames.OfflineDbWriteCompleted);
        completedIndex.Should().BeGreaterThan(0);
        logger.Events[completedIndex - 1].EventName.Should().Be(DiagnosticEventNames.OfflineDbWriteStarted);
    }

    private sealed class RecordingDiagnosticLogger : IDiagnosticLogger
    {
        public List<(DiagnosticLevel Level, string EventName)> Events { get; } = [];

        public Task LogAsync(
            DiagnosticLevel level,
            string eventName,
            string message,
            IReadOnlyDictionary<string, string>? metadata = null,
            Exception? exception = null,
            CancellationToken cancellationToken = default)
        {
            Events.Add((level, eventName));
            return Task.CompletedTask;
        }
    }
}
