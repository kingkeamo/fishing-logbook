namespace FishingLogBook.Mobile.Presentation;

public static class LoaderPlayback
{
    public static bool ShouldAnimate(bool isLoaded, bool isRunning, bool isVisible, bool isPaused, bool windowActive, bool reduceMotion)
    {
        return isLoaded && isRunning && isVisible && !isPaused && windowActive && !reduceMotion;
    }
}
