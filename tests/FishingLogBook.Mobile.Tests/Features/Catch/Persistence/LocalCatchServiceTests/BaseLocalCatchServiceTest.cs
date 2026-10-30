using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Persistence.Enums;
using FishingLogBook.Mobile.Features.Catch.Persistence.Models;
using FishingLogBook.Mobile.Features.Catch.Persistence.Services;
using FishingLogBook.Mobile.Features.Catch.Persistence.Stores;
using FishingLogBook.Mobile.Features.LocalPersistence.Sqlite;
using FishingLogBook.Mobile.Features.LocalPersistence.Stores;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Persistence.LocalCatchServiceTests;

public abstract class BaseLocalCatchServiceTest : IDisposable
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
    protected readonly SqliteLocalCatchStore CatchStore;
    protected readonly LocalCatchService Sut;
    protected readonly AccountSessionLeaseModel OwnerLease;

    private readonly string _directory;
    private bool _disposed;

    protected BaseLocalCatchServiceTest()
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
        CatchStore = new SqliteLocalCatchStore(ConnectionFactory, Database);
        Sut = new LocalCatchService(
            MockSessionCoordinator,
            Database,
            CatchStore,
            OutboxStore,
            TimeProvider.System,
            NullLogger<LocalCatchService>.Instance);
    }

    protected LocalCatchService CreateServiceForUser(Guid userId)
    {
        var lease = new AccountSessionLeaseModel(userId, 1, CancellationToken.None);
        var session = Substitute.For<IAccountSessionCoordinator>();
        session.Capture().Returns(lease);
        session.IsCurrent(Arg.Is<AccountSessionLeaseModel>(value => value.UserId == userId)).Returns(true);
        return new LocalCatchService(
            session,
            Database,
            CatchStore,
            OutboxStore,
            TimeProvider.System,
            NullLogger<LocalCatchService>.Instance);
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

    protected LocalCatchService RecreateServiceAgainstSameDatabase()
    {
        var factory = new SqliteConnectionFactory(DatabasePath);
        var migrator = new SqliteMigrator(factory, NullLogger<SqliteMigrator>.Instance);
        var database = new SqliteDatabase(factory, migrator, NullLogger<SqliteDatabase>.Instance);
        var outboxStore = new SqliteOutboxStore(factory, database);
        var catchStore = new SqliteLocalCatchStore(factory, database);
        return new LocalCatchService(
            MockSessionCoordinator,
            database,
            catchStore,
            outboxStore,
            TimeProvider.System,
            NullLogger<LocalCatchService>.Instance);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Database.Dispose();
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
