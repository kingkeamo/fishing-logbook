namespace FishingLogBook.Web.Browser.Install;

public interface IInstallService
{
    Task<InstallState> GetStateAsync(CancellationToken cancellationToken);

    Task<InstallResult> PromptAsync(CancellationToken cancellationToken);

    Task<IAsyncDisposable> SubscribeAsync(
        Func<InstallState, Task> onStateChanged,
        CancellationToken cancellationToken);
}
