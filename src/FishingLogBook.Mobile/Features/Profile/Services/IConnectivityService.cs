namespace FishingLogBook.Mobile.Features.Profile.Services;

public interface IConnectivityService
{
    bool IsOnline { get; }
    event EventHandler? ConnectivityChanged;
}
