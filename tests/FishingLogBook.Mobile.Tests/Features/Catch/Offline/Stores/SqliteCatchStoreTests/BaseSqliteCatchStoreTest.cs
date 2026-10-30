using FishingLogBook.Mobile.Common.Offline.Sqlite;
using FishingLogBook.Mobile.Common.Offline.Stores;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Offline.Stores.SqliteCatchStoreTests;

[Collection(FishingLogBook.Mobile.Tests.Common.Offline.SqliteTestCollection.Name)]
public abstract class BaseSqliteCatchStoreTest : IDisposable
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected readonly string DatabasePath;
    protected readonly IAccountSessionCoordinator MockSessionCoordinator =
        Substitute.For<IAccountSessionCoordinator>();
    protected readonly SqliteConnectionFactory ConnectionFactory;
    protected readonly SqliteMigrator Migrator;
    protected readonly SqliteDatabase Database;
    protected readonly SqliteOutboxStore OutboxStore;
    protected readonly SqliteCatchStore Sut;
    protected readonly AccountSessionLeaseModel OwnerLease;

    private readonly string _directory;
    private bool _disposed;

    protected BaseSqliteCatchStoreTest()
    {
        _directory = Path.Combine(Path.GetTempPath(), "flb-sqlite-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        DatabasePath = Path.Combine(_directory, "local-logbook.db");
        OwnerLease = new AccountSessionLeaseModel(OwnerUserId, 1, CancellationToken.None);
        MockSessionCoordinator.Capture().Returns(OwnerLease);
        MockSessionCoordinator.IsCurrent(Arg.Is<AccountSessionLeaseModel>(lease => lease.UserId == OwnerUserId))
            .Returns(true);

        ConnectionFactory = new SqliteConnectionFactory(DatabasePath);
        Migrator = new SqliteMigrator(ConnectionFactory, NullLogger<SqliteMigrator>.Instance);
        Database = new SqliteDatabase(ConnectionFactory, Migrator, NullLogger<SqliteDatabase>.Instance);
        OutboxStore = new SqliteOutboxStore(ConnectionFactory, Database);
        Sut = new SqliteCatchStore(
            MockSessionCoordinator,
            ConnectionFactory,
            Database,
            OutboxStore,
            TimeProvider.System,
            NullLogger<SqliteCatchStore>.Instance);
    }

    protected SqliteCatchStore CreateStoreForUser(Guid userId)
    {
        var lease = new AccountSessionLeaseModel(userId, 1, CancellationToken.None);
        var session = Substitute.For<IAccountSessionCoordinator>();
        session.Capture().Returns(lease);
        session.IsCurrent(Arg.Is<AccountSessionLeaseModel>(value => value.UserId == userId)).Returns(true);
        return new SqliteCatchStore(
            session,
            ConnectionFactory,
            Database,
            OutboxStore,
            TimeProvider.System,
            NullLogger<SqliteCatchStore>.Instance);
    }

    protected static LocalCatchSaveArgs CreateSaveArgs(
        Guid? catchId = null,
        string? speciesName = "Brown trout",
        double? weight = 1.2,
        Guid? tripId = null,
        string? tripTitle = null,
        LocalCatchLifecycleEnum lifecycle = LocalCatchLifecycleEnum.Saved)
    {
        return new LocalCatchSaveArgs
        {
            Id = catchId ?? Guid.NewGuid(),
            TripId = tripId,
            TripTitle = tripTitle,
            CaughtOn = DateTimeOffset.Parse("2026-05-01T12:00:00Z"),
            SpeciesName = speciesName,
            Weight = weight,
            Length = 42,
            Method = "Fly",
            BaitOrLure = "Dry fly",
            Notes = "Evening rise",
            PlaceName = "Lough Corrib",
            Lifecycle = lifecycle,
            Photographs =
            [
                new LocalCatchPhotographSaveArgs
                {
                    Id = Guid.NewGuid(),
                    SortOrder = 0,
                    LocalFilePath = "photos/one.jpg",
                    ContentType = "image/jpeg"
                }
            ]
        };
    }

    protected SqliteCatchStore RecreateStoreAgainstSameDatabase()
    {
        var factory = new SqliteConnectionFactory(DatabasePath);
        var migrator = new SqliteMigrator(factory, NullLogger<SqliteMigrator>.Instance);
        var database = new SqliteDatabase(factory, migrator, NullLogger<SqliteDatabase>.Instance);
        var outboxStore = new SqliteOutboxStore(factory, database);
        return new SqliteCatchStore(
            MockSessionCoordinator,
            factory,
            database,
            outboxStore,
            TimeProvider.System,
            NullLogger<SqliteCatchStore>.Instance);
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
