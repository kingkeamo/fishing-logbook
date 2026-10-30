using FishingLogBook.Mobile.Features.LocalPersistence.Sqlite;

namespace FishingLogBook.Mobile;

public sealed class AndroidNoBackupLocalDatabasePathProvider : ILocalDatabasePathProvider
{
    public string GetDatabasePath()
    {
        var directory = Android.App.Application.Context.NoBackupFilesDir
            ?? throw new InvalidOperationException("Android NoBackupFilesDir is unavailable.");
        return Path.Combine(directory.AbsolutePath, "local-logbook.db");
    }
}
