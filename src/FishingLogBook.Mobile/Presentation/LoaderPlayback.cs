namespace FishingLogBook.Mobile.Presentation;

public static class LoaderPlayback
{
    public static bool ShouldAnimate(bool isLoaded, bool isRunning, bool isPaused, bool windowActive, bool reduceMotion)
    {
        return isLoaded && isRunning && !isPaused && windowActive && !reduceMotion;
    }
}
