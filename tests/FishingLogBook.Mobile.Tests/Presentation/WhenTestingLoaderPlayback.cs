using AwesomeAssertions;
using FishingLogBook.Mobile.Presentation;

namespace FishingLogBook.Mobile.Tests.Presentation;

public class WhenTestingLoaderPlayback
{
    [Theory]
    [InlineData(false, true, true, false, true, false)]
    [InlineData(true, false, true, false, true, false)]
    [InlineData(true, true, false, false, true, false)]
    [InlineData(true, true, true, true, true, false)]
    [InlineData(true, true, true, false, false, false)]
    [InlineData(true, true, true, false, true, true)]
    public void ItShouldNotAnimateWhenAnyConditionBlocksIt(bool loaded, bool running, bool visible, bool paused, bool windowActive, bool reduceMotion)
    {
        // Arrange
        // Act
        var result = LoaderPlayback.ShouldAnimate(loaded, running, visible, paused, windowActive, reduceMotion);
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ItShouldNotAnimateWhenTheLoaderIsHiddenSoItDoesNotUseCpuOrBattery()
    {
        // Arrange
        // Act
        var result = LoaderPlayback.ShouldAnimate(true, true, false, false, true, false);
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ItShouldAnimateWhenLoadedRunningVisibleAndActive()
    {
        // Arrange
        // Act
        var result = LoaderPlayback.ShouldAnimate(true, true, true, false, true, false);
        // Assert
        result.Should().BeTrue();
    }
}
