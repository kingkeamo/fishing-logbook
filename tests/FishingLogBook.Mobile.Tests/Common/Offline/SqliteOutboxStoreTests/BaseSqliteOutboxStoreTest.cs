using FishingLogBook.Mobile.Common.Offline.Sqlite;
using FishingLogBook.Mobile.Common.Offline.Stores;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Mobile.Tests.Common.Offline.SqliteOutboxStoreTests;

public abstract class BaseSqliteOutboxStoreTest : IDisposable
{
    protected static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected readonly SqliteConnectionFactory ConnectionFactory;
    protected readonly SqliteDatabase Database;
    protected readonly SqliteOutboxStore Sut;

    private readonly string _directory;
    private bool _disposed;

    protected BaseSqliteOutboxStoreTest()
    {
        _directory = Path.Combine(Path.GetTempPath(), "flb-sqlite-outbox", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        var databasePath = Path.Combine(_directory, "local-logbook.db");
        ConnectionFactory = new SqliteConnectionFactory(databasePath);
        var migrator = new SqliteMigrator(ConnectionFactory, NullLogger<SqliteMigrator>.Instance);
        Database = new SqliteDatabase(ConnectionFactory, migrator, NullLogger<SqliteDatabase>.Instance);
        Sut = new SqliteOutboxStore(ConnectionFactory, Database);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Database.Dispose();
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
