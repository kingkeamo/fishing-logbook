using FishingLogBook.Mobile.Features.OfflineAccess.Models;

namespace FishingLogBook.Mobile.Features.OfflineAccess.Stores;

public interface IOfflineAccountStore
{
    Task<OfflineAccountModel?> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task SaveAsync(OfflineAccountModel account, CancellationToken cancellationToken);
}
