namespace FishingLogBook.Web.Browser.Network;

public interface INetworkService
{
    event Action<bool>? ConnectivityChanged;

    Task<bool> IsOnlineAsync(CancellationToken cancellationToken);

    Task StartMonitoringAsync(CancellationToken cancellationToken);
}
