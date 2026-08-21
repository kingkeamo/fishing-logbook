namespace FishingLogBook.Web.Browser.Install;

public interface IInstallService
{
    Task<InstallState> GetStateAsync(CancellationToken cancellationToken);

    Task<InstallResult> PromptAsync(CancellationToken cancellationToken);
}

public sealed record InstallState(
    bool IsInstalled,
    bool CanPrompt,
    string PlatformFamily,
    bool IsSafari)
{
    public InstallState(bool isInstalled, bool canPrompt, bool isIos, bool isAndroid)
        : this(
            isInstalled,
            canPrompt,
            isIos ? InstallPlatformFamilies.Ios : isAndroid ? InstallPlatformFamilies.Android : InstallPlatformFamilies.Other,
            isIos)
    {
    }

    public bool IsIos => PlatformFamily == InstallPlatformFamilies.Ios;
    public bool IsAndroid => PlatformFamily == InstallPlatformFamilies.Android;
    public bool IsWindows => PlatformFamily == InstallPlatformFamilies.Windows;
}

public enum InstallResult
{
    Unavailable,
    Accepted,
    Dismissed
}

public static class InstallPlatformFamilies
{
    public const string Ios = "iOS";
    public const string Android = "Android";
    public const string Windows = "Windows";
    public const string Other = "Other";
}
