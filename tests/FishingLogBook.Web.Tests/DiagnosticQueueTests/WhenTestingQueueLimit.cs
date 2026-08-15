using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Diagnostics;
using FishingLogBook.Web.Tests.DiagnosticLoggerTests;

namespace FishingLogBook.Web.Tests.DiagnosticQueueTests;

public class WhenTestingQueueLimit
{
    [Fact]
    public async Task ItShouldDiscardTheOldestEvents_WhenTheMaximumIsExceeded()
    {
        // Arrange
        var store = new MemoryDiagnosticEventStore { MaxQueueSize = 3 };
        for (var index = 0; index < 3; index++)
        {
            await store.EnqueueAsync(
                new DiagnosticEvent
                {
                    Id = Guid.NewGuid(),
                    TimestampUtc = DateTimeOffset.UtcNow.AddMinutes(index),
                    EventName = $"Event{index}",
                    Level = DiagnosticLevel.Warning
                },
                CancellationToken.None);
        }

        // Act
        await store.EnqueueAsync(
            new DiagnosticEvent
            {
                Id = Guid.NewGuid(),
                TimestampUtc = DateTimeOffset.UtcNow.AddMinutes(3),
                EventName = "Event3",
                Level = DiagnosticLevel.Warning
            },
            CancellationToken.None);

        // Assert
        store.Items.Should().HaveCount(3);
        store.Items.Select(item => item.EventName).Should().Equal("Event1", "Event2", "Event3");
    }
}
