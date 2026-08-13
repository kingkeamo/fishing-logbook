namespace FishingLogBook.Application.Contracts;

public interface IDatabaseMigrator
{
    DatabaseMigrationResult Migrate();
}
