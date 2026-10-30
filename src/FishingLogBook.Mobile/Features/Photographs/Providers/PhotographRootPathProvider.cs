using FishingLogBook.Mobile.Common.Offline.Sqlite;
using FishingLogBook.Mobile.IO.Adapters;

namespace FishingLogBook.Mobile.Features.Photographs.Providers;

public sealed class PhotographRootPathProvider(
    IFileSystemAdapter fileSystem,
    Func<string> appDataDirectory,
    ILocalDatabasePathProvider? localDatabasePathProvider = null) : IPhotographRootPathProvider
{
    public string GetPhotographRootDirectory()
    {
        var baseDirectory = localDatabasePathProvider is null
            ? appDataDirectory()
            : Path.GetDirectoryName(localDatabasePathProvider.GetDatabasePath()) ?? appDataDirectory();
        var root = Path.Combine(baseDirectory, "photographs");
        fileSystem.CreateDirectory(root);
        return root;
    }
}
