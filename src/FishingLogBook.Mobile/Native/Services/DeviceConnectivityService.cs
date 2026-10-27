using FishingLogBook.Mobile.Features.Profile.Services;

namespace FishingLogBook.Mobile.Native.Services;

public sealed class DeviceConnectivityService : IConnectivityService
{
    public DeviceConnectivityService()
    {
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    public bool IsOnline => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
    public event EventHandler? ConnectivityChanged;

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs args)
    {
        ConnectivityChanged?.Invoke(this, EventArgs.Empty);
    }
}
