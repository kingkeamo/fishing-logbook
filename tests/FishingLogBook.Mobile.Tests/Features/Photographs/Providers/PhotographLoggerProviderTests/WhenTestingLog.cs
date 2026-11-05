using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Photographs.Providers;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.Providers.PhotographLoggerProviderTests;

public class WhenTestingLog
{
    [Theory]
    [InlineData("FishingLogBook.Mobile.Features.Photographs.Services.PhotographPreparationService")]
    [InlineData("FishingLogBook.Mobile.Features.Photographs.Storage.Stores.PhotographFileStore")]
    [InlineData("FishingLogBook.Mobile.Features.Photographs.ViewModels.PhotographCollectionEditorViewModel")]
    [InlineData("FishingLogBook.Mobile.Features.Trips.Services.TripPhotographService")]
    [InlineData("FishingLogBook.Mobile.Native.Adapters.PhotographMediaAdapter")]
    public void ShouldEmitPipelineEventsAndAnyExceptionButNotOtherUnmarkedMessages(string category)
    {
        // Arrange
        var messages = new List<string>();
        using var provider = new PhotographLoggerProvider(messages.Add);
        var logger = provider.CreateLogger(category);

        // Act
        logger.LogWarning(PhotographLoggerProvider.PipelineEvent, "Pipeline step {Stage}.", "atomic move");
        logger.LogError(new IOException("boom"), "Unmarked failure.");
        logger.LogWarning("PRIVATE-UNMARKED-EVENT");
        logger.LogDebug(PhotographLoggerProvider.PipelineEvent, "PRIVATE-DEBUG");
        logger.Log(LogLevel.None, PhotographLoggerProvider.PipelineEvent, "PRIVATE-NONE");
        provider.CreateLogger("Other.Category").LogError(new IOException("PRIVATE-OTHER"), "PRIVATE-OTHER-CATEGORY");

        // Assert
        messages.Should().HaveCount(2);
        messages[0].Should().Be("FishingLogBook photograph: Pipeline step atomic move.");
        messages[1].Should().StartWith("FishingLogBook photograph: Unmarked failure.");
    }

    [Fact]
    public void ShouldWriteTheExceptionTypeMessageAndStackTrace()
    {
        // Arrange
        var messages = new List<string>();
        using var provider = new PhotographLoggerProvider(messages.Add);
        var logger = provider.CreateLogger("FishingLogBook.Mobile.Features.Photographs.ViewModels.PhotographCollectionEditorViewModel");
        var exception = Capture(() => throw new InvalidOperationException("prepare exploded", new IOException("inner cause")));

        // Act
        logger.LogError(exception, "Failed to prepare photograph.");

        // Assert
        messages.Should().ContainSingle();
        messages[0].Should().Contain("Failed to prepare photograph.")
            .And.Contain(nameof(InvalidOperationException))
            .And.Contain("prepare exploded")
            .And.Contain(nameof(Capture))
            .And.Contain(nameof(IOException))
            .And.Contain("inner cause");
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

    private static Exception Capture(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected an exception.");
    }
}
