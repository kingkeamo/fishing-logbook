namespace FishingLogBook.Web.Browser.Network;

public interface INetworkService
{
    Task<bool> IsOnlineAsync(CancellationToken cancellationToken);
}
