namespace FishingLogBook.Mobile.Features.LocalPersistence.Sqlite;

public interface ISqliteMigrator
{
    Task MigrateAsync(CancellationToken cancellationToken);
}
