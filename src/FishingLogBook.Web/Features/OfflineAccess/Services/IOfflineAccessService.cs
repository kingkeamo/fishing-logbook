using FishingLogBook.Web.Features.OfflineAccess.Models;

namespace FishingLogBook.Web.Features.OfflineAccess.Services;

public interface IOfflineAccessService
{
    Task<OfflineAccessStatusModel> GetStatusAsync(CancellationToken cancellationToken);
    Task<OfflineAccessStatusModel> SetupAsync(CancellationToken cancellationToken);
    Task<OfflineAccessStatusModel> RemoveFromDeviceAsync(CancellationToken cancellationToken);
    Task<OfflineAccessStatusModel> TurnOffAccountAsync(CancellationToken cancellationToken);
}
