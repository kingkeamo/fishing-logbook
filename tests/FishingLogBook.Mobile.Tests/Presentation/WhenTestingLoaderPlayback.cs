using AwesomeAssertions;
using FishingLogBook.Mobile.Presentation;

namespace FishingLogBook.Mobile.Tests.Presentation;

public class WhenTestingLoaderPlayback
{
    [Fact]
    public void ItShouldAnimateWhenLoadedRunningVisibleAndActive()
    {
        // Act
        var result = LoaderPlayback.ShouldAnimate(true, true, false, true, false);
        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(false, true, false, true, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(true, true, true, true, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, true, false, true, true)]
    public void ItShouldNotAnimateWhenAnyConditionBlocksIt(bool loaded, bool running, bool paused, bool windowActive, bool reduceMotion)
    {
        // Act
        var result = LoaderPlayback.ShouldAnimate(loaded, running, paused, windowActive, reduceMotion);
        // Assert
        result.Should().BeFalse();
    }
}
