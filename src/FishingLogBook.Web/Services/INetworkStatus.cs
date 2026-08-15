namespace FishingLogBook.Web.Services;

public interface INetworkStatus
{
    Task<bool> IsOnlineAsync(CancellationToken cancellationToken);
}
