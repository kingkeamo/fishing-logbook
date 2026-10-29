using FishingLogBook.Mobile.Features.OfflineAccess.Models;

namespace FishingLogBook.Mobile.Features.OfflineAccess.Stores;

public interface ISecureOfflineCapabilityStore
{
    Task<OfflineCapabilityModel?> GetAsync(CancellationToken cancellationToken);
    Task SaveAsync(OfflineCapabilityModel capability, CancellationToken cancellationToken);
    Task RemoveAsync(CancellationToken cancellationToken);
}
