using FishingLogBook.Mobile.Features.LocalPersistence.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Mobile.Tests.Features.LocalPersistence.SqliteMigratorTests;

public abstract class BaseSqliteMigratorTest : IDisposable
{
    protected readonly string DatabasePath;
    protected readonly SqliteConnectionFactory ConnectionFactory;
    protected readonly SqliteMigrator Sut;

    private readonly string _directory;
    private bool _disposed;

    protected BaseSqliteMigratorTest()
    {
        _directory = Path.Combine(Path.GetTempPath(), "flb-sqlite-migrate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        DatabasePath = Path.Combine(_directory, "local-logbook.db");
        ConnectionFactory = new SqliteConnectionFactory(DatabasePath);
        Sut = new SqliteMigrator(ConnectionFactory, NullLogger<SqliteMigrator>.Instance);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        GC.SuppressFinalize(this);
    }
}
