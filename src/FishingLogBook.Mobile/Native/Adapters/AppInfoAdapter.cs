using System.Reflection;
using FishingLogBook.Mobile.IO.Adapters;

namespace FishingLogBook.Mobile.Native.Adapters;

public sealed class AppInfoAdapter : IAppInfoAdapter
{
    public string Version => AppInfo.Current.VersionString;

    public string Build => AppInfo.Current.BuildString;

    public string Platform => DeviceInfo.Current.Platform.ToString();

    public string PlatformVersion => DeviceInfo.Current.VersionString;

    public string? Commit => ReadCommit();

    private static string? ReadCommit()
    {
        var informational = typeof(AppInfoAdapter).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational))
        {
            return null;
        }

        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        if (plus < 0 || plus == informational.Length - 1)
        {
            return null;
        }

        var commit = informational[(plus + 1)..].Trim();
        return commit.Length == 0 ? null : commit;
    }
}
