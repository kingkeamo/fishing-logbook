using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Sqlite;
using FishingLogBook.Mobile.Features.Photographs.Offline.Stores;
using FishingLogBook.Mobile.Features.Photographs.Providers;
using FishingLogBook.Mobile.Features.Photographs.Services;
using FishingLogBook.Mobile.IO.Adapters;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Mobile.Tests.Common.Offline.Sqlite.SqliteDatabaseTests;

public sealed class WhenTestingInitialization : IDisposable
{
    private readonly string _directory;
    private readonly string _databasePath;
    private readonly string _photoRoot;
    private readonly Guid _userId = Guid.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

    public WhenTestingInitialization()
    {
        _directory = Path.Combine(Path.GetTempPath(), "flb-sqlite-init-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _databasePath = Path.Combine(_directory, "local-logbook.db");
        _photoRoot = Path.Combine(_directory, "photographs");
        var accountDirectory = Path.Combine(_photoRoot, _userId.ToString("N"));
        Directory.CreateDirectory(accountDirectory);
        File.WriteAllBytes(Path.Combine(accountDirectory, $"{Guid.NewGuid():N}.jpg"), [0xFF, 0xD8, 0xFF, 0xD9]);
    }

    [Fact]
    public async Task ShouldCompleteWhenInitializedObserverQueriesSqliteWithExistingAccountPhotoDirectory()
    {
        // Arrange
        var connectionFactory = new SqliteConnectionFactory(_databasePath);
        var migrator = new SqliteMigrator(connectionFactory, NullLogger<SqliteMigrator>.Instance);
        SqliteDatabase? database = null;
        var referenceStore = new SqlitePhotographReferenceStore(
            connectionFactory,
            new DeferredSqliteDatabase(() => database!));
        var cleanup = new PhotographOrphanCleanupService(
            new FileSystemAdapter(),
            new FixedRoot(_photoRoot),
            referenceStore,
            new DeferredSqliteDatabase(() => database!),
            NullLogger<PhotographOrphanCleanupService>.Instance);
        database = new SqliteDatabase(
            connectionFactory,
            migrator,
            NullLogger<SqliteDatabase>.Instance,
            [cleanup]);

        // Act
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await database.EnsureInitializedAsync(timeout.Token).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        timeout.IsCancellationRequested.Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class FixedRoot(string root) : IPhotographRootPathProvider
    {
        public string GetPhotographRootDirectory() => root;
    }

    private sealed class DeferredSqliteDatabase(Func<ISqliteDatabase> resolve) : ISqliteDatabase
    {
        public Task EnsureInitializedAsync(CancellationToken cancellationToken)
        {
            return resolve().EnsureInitializedAsync(cancellationToken);
        }

        public Task ExecuteInTransactionAsync(
            Func<Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite.SqliteTransaction, CancellationToken, Task> action,
            CancellationToken cancellationToken)
        {
            return resolve().ExecuteInTransactionAsync(action, cancellationToken);
        }
    }
}
