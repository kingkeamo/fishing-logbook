namespace FishingLogBook.Mobile.Common.Offline.Sqlite;

public interface ISqliteMigrator
{
    Task MigrateAsync(CancellationToken cancellationToken);
}
