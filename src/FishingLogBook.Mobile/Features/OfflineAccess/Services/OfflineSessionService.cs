using FishingLogBook.Mobile.Features.OfflineAccess.Models;

namespace FishingLogBook.Mobile.Features.OfflineAccess.Services;

public sealed class OfflineSessionService : IOfflineSessionService
{
    private readonly object _stateLock = new();
    private OfflineCapabilityModel? _current;

    public OfflineCapabilityModel? Current
    {
        get
        {
            lock (_stateLock)
            {
                return _current;
            }
        }
    }

    public void Activate(OfflineCapabilityModel capability)
    {
        ArgumentNullException.ThrowIfNull(capability);
        lock (_stateLock)
        {
            _current = capability;
        }
    }

    public bool Lock()
    {
        lock (_stateLock)
        {
            var wasActive = _current is not null;
            _current = null;
            return wasActive;
        }
    }
}
