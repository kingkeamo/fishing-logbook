using FishingLogBook.Web.Features.OfflineAccess.Models;

namespace FishingLogBook.Web.Features.OfflineAccess.Services;

public sealed class OfflineOwnerContextService : IOfflineOwnerContextService
{
    public OfflineOwnerModel? Owner { get; private set; }

    public bool IsUnlocked => Owner is not null;

    public void Unlock(OfflineOwnerModel owner)
    {
        Owner = owner;
    }

    public void Lock()
    {
        Owner = null;
    }
}

