using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Features.Diagnostics.Services;

namespace FishingLogBook.Web.Tests.Common.Offline.OfflineOperationTests;

public class BaseOfflineOperationTest
{
    protected sealed class RecordingDiagnosticLogger : IDiagnosticLogger
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

    protected static async Task WaitForEventAsync(RecordingDiagnosticLogger logger, string eventName)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (logger.Events.Exists(item => item.EventName == eventName))
            {
                return;
            }

            await Task.Delay(10);
        }

        logger.Events.Should().Contain(item => item.EventName == eventName);
    }
}
