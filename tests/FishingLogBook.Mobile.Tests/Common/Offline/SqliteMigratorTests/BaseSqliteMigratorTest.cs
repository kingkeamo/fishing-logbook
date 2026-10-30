using FishingLogBook.Mobile.Common.Offline.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Mobile.Tests.Common.Offline.SqliteMigratorTests;

[Collection(SqliteTestCollection.Name)]
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
        TryDeleteDirectory(_directory);
        GC.SuppressFinalize(this);
    }

    private static void TryDeleteDirectory(string directory)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }

                return;
            }
            catch (IOException)
            {
                Thread.Sleep(25);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(25);
            }
        }
    }
}
