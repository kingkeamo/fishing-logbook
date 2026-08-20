namespace FishingLogBook.Web.Browser.Install;

public interface IInstallService
{
    Task<InstallState> GetStateAsync(CancellationToken cancellationToken);

    Task<bool> PromptAsync(CancellationToken cancellationToken);
}

public sealed record InstallState(bool IsInstalled, bool CanPrompt, bool IsIos, bool IsAndroid);
