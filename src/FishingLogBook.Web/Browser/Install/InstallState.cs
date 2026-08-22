namespace FishingLogBook.Web.Browser.Install;

public sealed record InstallState(
    bool IsInstalled,
    bool CanPrompt,
    string PlatformFamily,
    bool IsSafari)
{
    public static InstallState Unknown { get; } =
        new(false, false, InstallPlatformFamilies.Other, false);

    public bool IsIos => PlatformFamily == InstallPlatformFamilies.Ios;

    public bool IsAndroid => PlatformFamily == InstallPlatformFamilies.Android;

    public bool IsDesktop => PlatformFamily == InstallPlatformFamilies.Desktop;
}
