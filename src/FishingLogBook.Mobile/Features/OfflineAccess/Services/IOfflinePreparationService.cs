using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.OfflineAccess.Services;

public interface IOfflinePreparationService
{
    Task<bool> EnsurePreparedAsync(ProfileDto profile, CancellationToken cancellationToken);
    Task<bool> IsPreparedAsync(OfflineCapabilityModel capability, CancellationToken cancellationToken);
}
