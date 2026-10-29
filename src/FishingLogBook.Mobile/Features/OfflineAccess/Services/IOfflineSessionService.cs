using FishingLogBook.Mobile.Features.OfflineAccess.Models;

namespace FishingLogBook.Mobile.Features.OfflineAccess.Services;

public interface IOfflineSessionService
{
    OfflineCapabilityModel? Current { get; }
    void Activate(OfflineCapabilityModel capability);
    bool Lock();
}
