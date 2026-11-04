namespace FishingLogBook.Mobile.IO.Adapters;

public interface IAppInfoAdapter
{
    string Version { get; }

    string Build { get; }

    string Platform { get; }

    string PlatformVersion { get; }

    string? Commit { get; }

    void ShowSettings();
}
