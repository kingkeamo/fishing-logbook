using FishingLogBook.Web.Features.OfflineAccess.Models;

namespace FishingLogBook.Web.Features.OfflineAccess.Services;

public interface IOfflineOwnerContextService
{
    OfflineOwnerModel? Owner { get; }
    bool IsUnlocked { get; }
    void Unlock(OfflineOwnerModel owner);
    void Lock();
}
