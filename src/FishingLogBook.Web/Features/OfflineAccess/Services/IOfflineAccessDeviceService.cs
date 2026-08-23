using FishingLogBook.Web.Features.OfflineAccess.Models;

namespace FishingLogBook.Web.Features.OfflineAccess.Services;

public interface IOfflineAccessDeviceService
{
    Task<OfflineAccessDeviceResultModel> GetStatusAsync(OfflineAccessIdentityModel identity, CancellationToken cancellationToken);
    Task<OfflineAccessDeviceResultModel> SetupAsync(OfflineAccessIdentityModel identity, CancellationToken cancellationToken);
    Task RemoveAsync(OfflineAccessIdentityModel identity, CancellationToken cancellationToken);
}
