using FishingLogBook.Mobile.Features.Profile.Services;

namespace FishingLogBook.Mobile.Native.Services;

public sealed class DeviceConnectivityService : IConnectivityService
{
    private readonly object _stateLock = new();
    private bool _isOnline;

    public DeviceConnectivityService()
    {
        _isOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    public bool IsOnline
    {
        get
        {
            lock (_stateLock)
            {
                return _isOnline;
            }
        }
    }

    public event EventHandler? ConnectivityChanged;

    public bool TryRunWhileOnline(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_stateLock)
        {
            if (!_isOnline)
            {
                return false;
            }
        }

        action();

        lock (_stateLock)
        {
            _isOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
            return _isOnline;
        }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs args)
    {
        lock (_stateLock)
        {
            _isOnline = args.NetworkAccess == NetworkAccess.Internet;
        }

        ConnectivityChanged?.Invoke(this, EventArgs.Empty);
    }
}
