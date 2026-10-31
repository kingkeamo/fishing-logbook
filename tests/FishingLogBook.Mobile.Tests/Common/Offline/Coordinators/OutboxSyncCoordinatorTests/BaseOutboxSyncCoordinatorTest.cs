using System.Globalization;
using FishingLogBook.Mobile.Common.Offline.Coordinators;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Models;
using FishingLogBook.Mobile.Common.Offline.Services;
using FishingLogBook.Mobile.Common.Offline.Sqlite;
using FishingLogBook.Mobile.Common.Offline.Stores;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Common.Offline.Coordinators.OutboxSyncCoordinatorTests;

public abstract class BaseOutboxSyncCoordinatorTest : IDisposable
{
    protected const int MaximumAttempts = 3;
    protected static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid OtherUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    protected static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-17T10:00:00Z");
    protected static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    protected readonly SqliteConnectionFactory ConnectionFactory;
    protected readonly SqliteDatabase Database;
    protected readonly SqliteOutboxStore Store;
    protected readonly IAccountSessionCoordinator MockSession = Substitute.For<IAccountSessionCoordinator>();
    protected readonly IConnectivityService MockConnectivity = Substitute.For<IConnectivityService>();
    protected readonly ISessionLifecycleCoordinator MockLifecycle = Substitute.For<ISessionLifecycleCoordinator>();
    protected readonly IOutboxOperationSynchroniser MockSynchroniser = Substitute.For<IOutboxOperationSynchroniser>();
    protected readonly IOutboxChangeNotifier MockNotifier = Substitute.For<IOutboxChangeNotifier>();
    protected readonly TimeProvider MockTime = Substitute.ForPartsOf<TimeProvider>();
    protected readonly ITimer MockTimer = Substitute.For<ITimer>();
    protected readonly AccountSessionLeaseModel Lease = new(UserId, 1, CancellationToken.None);
    protected readonly OutboxSyncCoordinator Sut;

    private readonly string _directory;
    private int _seedCount;
    private bool _disposed;

    protected BaseOutboxSyncCoordinatorTest()
    {
        _directory = Path.Combine(Path.GetTempPath(), "flb-sqlite-outbox-sync", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        ConnectionFactory = new SqliteConnectionFactory(Path.Combine(_directory, "local-logbook.db"));
        var migrator = new SqliteMigrator(ConnectionFactory, NullLogger<SqliteMigrator>.Instance);
        Database = new SqliteDatabase(ConnectionFactory, migrator, NullLogger<SqliteDatabase>.Instance);
        Store = new SqliteOutboxStore(ConnectionFactory, Database);

        MockTime.When(time => time.CreateTimer(
                Arg.Any<TimerCallback>(),
                Arg.Any<object?>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<TimeSpan>()))
            .DoNotCallBase();
        MockTime.CreateTimer(
                Arg.Any<TimerCallback>(),
                Arg.Any<object?>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<TimeSpan>())
            .Returns(MockTimer);
        MockTime.GetUtcNow().Returns(Now);

        MockSession.Capture().Returns(Lease);
        MockSession.IsCurrent(Arg.Any<AccountSessionLeaseModel>()).Returns(true);
        MockConnectivity.IsOnline.Returns(true);
        MockLifecycle.State.Returns(SessionLifecycleStateEnum.Online);
        MockSynchroniser.OperationType.Returns(OutboxOperationTypeEnum.UpsertCatchMetadata);
        RespondWith(_ => Task.FromResult(OutboxOperationResultModel.Completed()));

        Sut = CreateSut(MockSynchroniser);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Sut.Dispose();
        Database.Dispose();
        TryDeleteDirectory(_directory);
        GC.SuppressFinalize(this);
    }

    protected OutboxSyncCoordinator CreateSut(params IOutboxOperationSynchroniser[] synchronisers)
    {
        return new OutboxSyncCoordinator(
            MockSession,
            MockConnectivity,
            MockLifecycle,
            Store,
            Database,
            synchronisers,
            MockNotifier,
            new OutboxBackoffPolicy(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(10), MaximumAttempts, () => 0.5),
            MockTime,
            NullLogger<OutboxSyncCoordinator>.Instance);
    }

    protected void RespondWith(Func<OutboxOperationModel, Task<OutboxOperationResultModel>> handler)
    {
        RespondWith((operation, _) => handler(operation));
    }

    protected void RespondWith(Func<OutboxOperationModel, CancellationToken, Task<OutboxOperationResultModel>> handler)
    {
        MockSynchroniser
            .SynchroniseAsync(
                Arg.Any<OutboxOperationModel>(),
                Arg.Any<AccountSessionLeaseModel>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => handler(callInfo.Arg<OutboxOperationModel>(), callInfo.Arg<CancellationToken>()));
    }

    protected async Task<Guid> SeedAsync(
        Guid? userId = null,
        DateTimeOffset? createdOn = null,
        DateTimeOffset? availableAt = null,
        int attemptCount = 0,
        OutboxOperationTypeEnum operationType = OutboxOperationTypeEnum.UpsertCatchMetadata)
    {
        var id = Guid.NewGuid();
        var created = createdOn ?? Now.AddMinutes(-100 + _seedCount++);
        await Database.ExecuteInTransactionAsync(
            (connection, transaction, token) => Store.UpsertPendingAsync(
                connection,
                transaction,
                new OutboxOperationModel
                {
                    Id = id,
                    UserId = userId ?? UserId,
                    AggregateType = OutboxAggregateTypeEnum.Catch,
                    AggregateId = Guid.NewGuid(),
                    OperationType = operationType,
                    PayloadJson = "{\"original\":true}",
                    Status = OutboxStatusEnum.Pending,
                    AttemptCount = attemptCount,
                    CreatedOn = created,
                    AvailableAt = availableAt ?? Now
                },
                token),
            CancellationToken.None);
        return id;
    }

    protected async Task ExecuteSqlAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await ConnectionFactory.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }

    protected async Task<CoordinatorOutboxRow> ReadRowAsync(Guid id)
    {
        await using var connection = await ConnectionFactory.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT status, attemptcount, availableat, lasterror, leaseowner, leaseexpiresat, payloadjson
            FROM outboxoperations
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Outbox row not found.");
        }

        return new CoordinatorOutboxRow(
            Enum.Parse<OutboxStatusEnum>(reader.GetString(0)),
            reader.GetInt32(1),
            ParseTimestamp(reader.GetString(2)),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : ParseTimestamp(reader.GetString(5)),
            reader.GetString(6));
    }

    protected static Func<SqliteConnection, SqliteTransaction, CancellationToken, Task> PersistPayload(
        Guid targetId,
        string payload)
    {
        return async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE outboxoperations SET payloadjson = $payload WHERE id = $id;";
            command.Parameters.AddWithValue("$payload", payload);
            command.Parameters.AddWithValue("$id", targetId.ToString("D"));
            await command.ExecuteNonQueryAsync(token);
        };
    }

    private static DateTimeOffset ParseTimestamp(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
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

    protected sealed record CoordinatorOutboxRow(
        OutboxStatusEnum Status,
        int AttemptCount,
        DateTimeOffset AvailableAt,
        string? LastError,
        string? LeaseOwner,
        DateTimeOffset? LeaseExpiresAt,
        string PayloadJson);
}
