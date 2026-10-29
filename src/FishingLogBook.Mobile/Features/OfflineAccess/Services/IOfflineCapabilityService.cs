using FishingLogBook.Mobile.Features.OfflineAccess.Models;

namespace FishingLogBook.Mobile.Features.OfflineAccess.Services;

public interface IOfflineCapabilityService
{
    Task<OfflineCapabilityModel?> GetReadyAsync(CancellationToken cancellationToken);
    Task MarkReadyAsync(string subject, Guid userId, CancellationToken cancellationToken);
    Task RevokeAsync(CancellationToken cancellationToken);
    Task RevokeIfOwnedByAnotherAccountAsync(
        string subject,
        Guid userId,
        CancellationToken cancellationToken);
}
