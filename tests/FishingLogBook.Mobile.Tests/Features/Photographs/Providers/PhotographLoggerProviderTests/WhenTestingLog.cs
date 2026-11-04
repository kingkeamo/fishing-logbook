using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Photographs.Providers;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.Providers.PhotographLoggerProviderTests;

public class WhenTestingLog
{
    [Theory]
    [InlineData("FishingLogBook.Mobile.Features.Photographs.Services.PhotographPreparationService")]
    [InlineData("FishingLogBook.Mobile.Features.Photographs.Storage.Stores.PhotographFileStore")]
    [InlineData("FishingLogBook.Mobile.Native.Adapters.PhotographMediaAdapter")]
    public void ShouldEmitOnlyExplicitPipelineEventsWithoutExceptionDetails(string category)
    {
        // Arrange
        var messages = new List<string>();
        using var provider = new PhotographLoggerProvider(messages.Add);
        var logger = provider.CreateLogger(category);
        var exception = new IOException("PRIVATE-PATH-GPS");

        // Act
        logger.LogWarning(PhotographLoggerProvider.PipelineEvent, exception, "Durable save failed at {Stage}.", "atomic move");
        logger.LogWarning("PRIVATE-UNMARKED-EVENT");
        logger.LogDebug(PhotographLoggerProvider.PipelineEvent, "PRIVATE-DEBUG");
        logger.Log(LogLevel.None, PhotographLoggerProvider.PipelineEvent, "PRIVATE-NONE");
        provider.CreateLogger("Other.Category").LogWarning(PhotographLoggerProvider.PipelineEvent, "PRIVATE-OTHER-CATEGORY");

        // Assert
        messages.Should().Equal("FishingLogBook photograph: Durable save failed at atomic move.");
    }

    [Fact]
    public void ShouldNotInterruptPhotoPreparationWhenDeviceConsoleFails()
    {
        // Arrange
        var writes = 0;
        using var provider = new PhotographLoggerProvider(_ =>
        {
            writes++;
            throw new IOException("Console unavailable");
        });
        var logger = provider.CreateLogger("FishingLogBook.Mobile.Features.Photographs.Services.PhotographPreparationService");

        // Act
        var act = () => logger.LogWarning(PhotographLoggerProvider.PipelineEvent, "Source read failed.");

        // Assert
        act.Should().NotThrow();
        writes.Should().Be(1);
    }
}
