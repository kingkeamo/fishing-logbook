namespace FishingLogBook.Web.Browser.Update;

public interface IAppUpdateService
{
    AppUpdateStatus Status { get; }

    event Action? StatusChanged;

    Task StartAsync(CancellationToken cancellationToken);

    Task ApplyAsync(CancellationToken cancellationToken);
}
