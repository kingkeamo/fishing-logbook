using FishingLogBook.Mobile.Common.Offline.Services;
using FishingLogBook.Mobile.Common.Offline.Sqlite;
using FishingLogBook.Mobile.Common.Offline.Stores;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Support;

/// <summary>A real temp-directory SQLite database wired to the real offline stores.</summary>
public sealed class SqliteOfflineHarness : IDisposable
{
    public static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly string _directory;
    private bool _disposed;

    public SqliteOfflineHarness()
    {
        _directory = Path.Combine(Path.GetTempPath(), "flb-sqlite-harness", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        ConnectionFactory = new SqliteConnectionFactory(Path.Combine(_directory, "local-logbook.db"));
        var migrator = new SqliteMigrator(ConnectionFactory, NullLogger<SqliteMigrator>.Instance);
        Database = new SqliteDatabase(ConnectionFactory, migrator, NullLogger<SqliteDatabase>.Instance);
        OutboxStore = new SqliteOutboxStore(ConnectionFactory, Database);
        StateStore = new SqliteCatchSyncStateStore(ConnectionFactory, Database);
        StatusStore = new SqliteCatchSyncStatusStore(ConnectionFactory, Database);
    }

    public SqliteConnectionFactory ConnectionFactory { get; }

    public SqliteDatabase Database { get; }

    public SqliteOutboxStore OutboxStore { get; }

    public SqliteCatchSyncStateStore StateStore { get; }

    public SqliteCatchSyncStatusStore StatusStore { get; }

    public IOutboxChangeNotifier Notifier { get; } = Substitute.For<IOutboxChangeNotifier>();

    public SqliteCatchStore CreateCatchStore(Guid userId)
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
            Notifier,
            TimeProvider.System,
            NullLogger<SqliteCatchStore>.Instance);
    }

    public static LocalCatchSaveArgs CreateSaveArgs(
        Guid? catchId = null,
        int photographCount = 1,
        LocalCatchLifecycleEnum lifecycle = LocalCatchLifecycleEnum.Saved,
        string? notes = "Evening rise")
    {
        return new LocalCatchSaveArgs
        {
            Id = catchId ?? Guid.NewGuid(),
            CaughtOn = DateTimeOffset.Parse("2026-05-01T12:00:00Z"),
            SpeciesName = "Brown trout",
            Weight = 1.2,
            Length = 42,
            Method = "Fly",
            BaitOrLure = "Dry fly",
            Notes = notes,
            PlaceName = "Lough Corrib",
            Lifecycle = lifecycle,
            Photographs = Enumerable.Range(0, photographCount)
                .Select(index => new LocalCatchPhotographSaveArgs
                {
                    Id = Guid.NewGuid(),
                    SortOrder = index,
                    LocalFilePath = $"photos/{index}.jpg",
                    ContentType = "image/jpeg"
                })
                .ToArray()
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Database.Dispose();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(_directory))
                {
                    Directory.Delete(_directory, recursive: true);
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
