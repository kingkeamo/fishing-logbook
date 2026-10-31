using System.Globalization;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Common.Offline.Coordinators;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Models;
using FishingLogBook.Mobile.Common.Offline.Services;
using FishingLogBook.Mobile.Common.Offline.Sqlite;
using FishingLogBook.Mobile.Common.Offline.Stores;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Clients;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using FishingLogBook.Mobile.Features.Catch.Offline.Synchronisers;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Shared.Dtos;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Offline.Synchronisers.CatchMetadataSynchroniserTests;

/// <summary>
/// End-to-end harness: real SQLite, real stores, real coordinator and real metadata synchroniser,
/// against a stateful in-memory stand-in for the server that sits behind an NSubstitute
/// <see cref="ICatchSyncClient"/>. The photograph synchroniser is an NSubstitute that returns
/// <c>Paused</c>: it releases the claimed photograph operation back to Pending (attempt not counted)
/// and stops the run, so enqueued photograph operations stay observable as Pending.
/// </summary>
public abstract class BaseCatchMetadataSynchroniserTest : IDisposable
{
    protected static readonly Guid UserA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid UserB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    protected static readonly DateTimeOffset StartTime = DateTimeOffset.Parse(
        "2026-06-01T10:00:00Z",
        CultureInfo.InvariantCulture);

    protected const int MaximumAttempts = 4;
    protected static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(15);

    protected readonly IAccountSessionCoordinator MockSession = Substitute.For<IAccountSessionCoordinator>();
    protected readonly IConnectivityService MockConnectivity = Substitute.For<IConnectivityService>();
    protected readonly ISessionLifecycleCoordinator MockLifecycle = Substitute.For<ISessionLifecycleCoordinator>();
    protected readonly IOutboxChangeNotifier MockNotifier = Substitute.For<IOutboxChangeNotifier>();
    protected readonly ICatchSyncClient MockClient = Substitute.For<ICatchSyncClient>();
    protected readonly IOutboxOperationSynchroniser MockPhotographSynchroniser =
        Substitute.For<IOutboxOperationSynchroniser>();

    protected readonly AdjustableClock Clock = new(StartTime);
    protected readonly SqliteConnectionFactory ConnectionFactory;
    protected readonly SqliteDatabase Database;
    protected readonly SqliteOutboxStore OutboxStore;
    protected readonly SqliteCatchStore CatchStore;
    protected readonly SqliteCatchSyncStateStore SyncStateStore;
    protected readonly SqliteCatchSyncStatusStore StatusStore;
    protected readonly CatchMetadataSynchroniser MetadataSynchroniser;
    protected readonly OutboxBackoffPolicy Backoff;
    protected readonly Dictionary<Guid, CatchDto> ServerCatches = new();

    private readonly string _directory;
    private readonly List<OutboxSyncCoordinator> _coordinators = [];
    private bool _disposed;

    protected BaseCatchMetadataSynchroniserTest()
    {
        _directory = Path.Combine(Path.GetTempPath(), "flb-sqlite-metadata-sync", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        var databasePath = Path.Combine(_directory, "local-logbook.db");

        CurrentUserId = UserA;
        Online = true;
        MockSession.Capture().Returns(_ => CurrentUserId is Guid userId
            ? new AccountSessionLeaseModel(userId, 1, CancellationToken.None)
            : null);
        MockSession.IsCurrent(Arg.Any<AccountSessionLeaseModel>())
            .Returns(call => call.Arg<AccountSessionLeaseModel>().UserId == CurrentUserId);
        MockConnectivity.IsOnline.Returns(_ => Online);

        ConnectionFactory = new SqliteConnectionFactory(databasePath);
        var migrator = new SqliteMigrator(ConnectionFactory, NullLogger<SqliteMigrator>.Instance);
        Database = new SqliteDatabase(ConnectionFactory, migrator, NullLogger<SqliteDatabase>.Instance);
        OutboxStore = new SqliteOutboxStore(ConnectionFactory, Database);
        CatchStore = new SqliteCatchStore(
            MockSession,
            ConnectionFactory,
            Database,
            OutboxStore,
            MockNotifier,
            Clock,
            NullLogger<SqliteCatchStore>.Instance);
        SyncStateStore = new SqliteCatchSyncStateStore(ConnectionFactory, Database);
        StatusStore = new SqliteCatchSyncStatusStore(ConnectionFactory, Database);
        MetadataSynchroniser = new CatchMetadataSynchroniser(
            CatchStore,
            MockClient,
            SyncStateStore,
            OutboxStore,
            Clock);
        Backoff = new OutboxBackoffPolicy(BaseDelay, TimeSpan.FromMinutes(15), MaximumAttempts, () => 0.5);

        MockPhotographSynchroniser.OperationType.Returns(OutboxOperationTypeEnum.UploadCatchPhotograph);
        MockPhotographSynchroniser
            .SynchroniseAsync(
                Arg.Any<OutboxOperationModel>(),
                Arg.Any<AccountSessionLeaseModel>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(OutboxOperationResultModel.Paused()));

        MockClient
            .UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ServerUpsertAsync(call.Arg<CatchDto>()));
        MockClient
            .DeletePhotographAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(ServerDeletePhotograph(call.ArgAt<Guid>(0), call.ArgAt<Guid>(1))));

        Coordinator = CreateCoordinator();
    }

    protected OutboxSyncCoordinator Coordinator { get; }

    /// <summary>The signed-in account the session substitute reports; null means signed out.</summary>
    protected Guid? CurrentUserId { get; set; }

    protected bool Online { get; set; }

    /// <summary>Set to make the stand-in server reject an upsert before storing anything.</summary>
    protected Func<CatchDto, ApiOutcomeEnum?>? UpsertRejection { get; set; }

    /// <summary>Set to make the stand-in server store the record and then lose the response.</summary>
    protected Func<CatchDto, ApiOutcomeEnum?>? UpsertLostResponse { get; set; }

    /// <summary>Awaited while an upsert request is "in flight", before the server stores the record.</summary>
    protected Func<CatchDto, Task>? UpsertInFlight { get; set; }

    /// <summary>Set to make the stand-in server fail a photograph DELETE.</summary>
    protected Func<Guid, Guid, ApiOutcomeEnum?>? DeleteRejection { get; set; }

    protected OutboxSyncCoordinator CreateCoordinator()
    {
        var coordinator = new OutboxSyncCoordinator(
            MockSession,
            MockConnectivity,
            MockLifecycle,
            OutboxStore,
            Database,
            [MetadataSynchroniser, MockPhotographSynchroniser],
            MockNotifier,
            Backoff,
            Clock,
            NullLogger<OutboxSyncCoordinator>.Instance);
        _coordinators.Add(coordinator);
        return coordinator;
    }

    protected Task RunAsync()
    {
        return Coordinator.RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);
    }

    protected static LocalCatchPhotographSaveArgs CreatePhotograph(
        int sortOrder,
        string? contentType = "image/jpeg",
        Guid? id = null)
    {
        return new LocalCatchPhotographSaveArgs
        {
            Id = id ?? Guid.NewGuid(),
            SortOrder = sortOrder,
            LocalFilePath = $"photos/{sortOrder}.jpg",
            ContentType = contentType
        };
    }

    protected static LocalCatchSaveArgs CreateSaveArgs(
        Guid catchId,
        IReadOnlyList<LocalCatchPhotographSaveArgs>? photographs = null,
        string? notes = "Evening rise",
        LocalCatchLifecycleEnum lifecycle = LocalCatchLifecycleEnum.Saved)
    {
        return new LocalCatchSaveArgs
        {
            Id = catchId,
            CaughtOn = DateTimeOffset.Parse("2026-05-01T12:00:00Z", CultureInfo.InvariantCulture),
            SpeciesName = "Brown trout",
            Weight = 1.2,
            Length = 42,
            Method = "Fly",
            BaitOrLure = "Dry fly",
            Notes = notes,
            PlaceName = "Lough Corrib",
            Lifecycle = lifecycle,
            Photographs = photographs ?? [CreatePhotograph(0, "image/jpeg"), CreatePhotograph(1, "image/png")]
        };
    }

    protected async Task<Guid> SaveCatchAsync(LocalCatchSaveArgs args)
    {
        var result = await CatchStore.SaveAsync(args, CancellationToken.None);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Arrange failed: the catch save returned {result.Outcome}.");
        }

        return args.Id;
    }

    protected async Task<IReadOnlyList<OutboxRow>> ReadOutboxAsync()
    {
        await using var connection = await ConnectionFactory.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, userid, aggregatetype, aggregateid, operationtype, status, attemptcount,
                   availableat, lasterror, payloadjson, leaseowner
            FROM outboxoperations
            ORDER BY createdon ASC, id ASC;
            """;
        var rows = new List<OutboxRow>();
        await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
        while (await reader.ReadAsync(CancellationToken.None))
        {
            rows.Add(new OutboxRow(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                Enum.Parse<OutboxAggregateTypeEnum>(reader.GetString(2)),
                Guid.Parse(reader.GetString(3)),
                Enum.Parse<OutboxOperationTypeEnum>(reader.GetString(4)),
                Enum.Parse<OutboxStatusEnum>(reader.GetString(5)),
                reader.GetInt32(6),
                DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10)));
        }

        return rows;
    }

    protected async Task<IReadOnlyList<OutboxRow>> ReadMetadataOperationsAsync(Guid catchId)
    {
        return (await ReadOutboxAsync())
            .Where(row => row.OperationType == OutboxOperationTypeEnum.UpsertCatchMetadata
                && row.AggregateId == catchId)
            .ToArray();
    }

    protected async Task<IReadOnlyList<OutboxRow>> ReadPhotographOperationsAsync()
    {
        return (await ReadOutboxAsync())
            .Where(row => row.OperationType == OutboxOperationTypeEnum.UploadCatchPhotograph)
            .ToArray();
    }

    protected async Task<long?> ReadSyncedRevisionAsync(Guid catchId)
    {
        await using var connection = await ConnectionFactory.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT syncedrevision FROM localcatchsyncs WHERE catchid = $catchId;";
        command.Parameters.AddWithValue("$catchId", catchId.ToString("D"));
        return await command.ExecuteScalarAsync(CancellationToken.None) is long revision ? revision : null;
    }

    protected async Task<CatchSyncStatusModel> ReadStatusAsync(Guid userId, Guid catchId)
    {
        var statuses = await StatusStore.ListAsync(userId, CancellationToken.None);
        return statuses[catchId];
    }

    protected async Task<LocalCatchRecordModel> ReadLocalCatchAsync(Guid userId, Guid catchId)
    {
        return (await CatchStore.GetForOwnerAsync(userId, catchId, CancellationToken.None))!;
    }

    protected Task MarkPhotographRecordedAsync(Guid userId, Guid catchId, Guid photographId)
    {
        return Database.ExecuteInTransactionAsync(
            (connection, transaction, token) => SyncStateStore.SetPhotographStateAsync(
                connection,
                transaction,
                new PhotographSyncStateModel
                {
                    PhotographId = photographId,
                    CatchId = catchId,
                    UserId = userId,
                    Status = PhotographSyncStatusEnum.Recorded,
                    ObjectKey = $"users/{userId:D}/catches/{catchId:D}/{photographId:D}"
                },
                Clock.GetUtcNow(),
                token),
            CancellationToken.None);
    }

    protected Task InsertPendingMetadataOperationAsync(Guid userId, Guid catchId)
    {
        return Database.ExecuteInTransactionAsync(
            (connection, transaction, token) => OutboxStore.UpsertPendingAsync(
                connection,
                transaction,
                new OutboxOperationModel
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    AggregateType = OutboxAggregateTypeEnum.Catch,
                    AggregateId = catchId,
                    OperationType = OutboxOperationTypeEnum.UpsertCatchMetadata,
                    PayloadJson = "{}",
                    Status = OutboxStatusEnum.Pending,
                    AttemptCount = 0,
                    CreatedOn = Clock.GetUtcNow(),
                    AvailableAt = Clock.GetUtcNow()
                },
                token),
            CancellationToken.None);
    }

    /// <summary>Simulates the server having stored a catch whose response never reached the app.</summary>
    protected void StoreOnServer(LocalCatchRecordModel local)
    {
        ServerCatches[local.Id] = new CatchDto(
            local.Id,
            local.CaughtOn,
            local.Photographs
                .OrderBy(photograph => photograph.SortOrder)
                .Select(photograph => new CatchPhotographDto(photograph.Id, local.Id, photograph.ContentType!))
                .ToArray())
        {
            CaughtByUserId = local.CaughtByUserId,
            RecordedByUserId = local.RecordedByUserId,
            Notes = local.Notes
        };
    }

    protected static TimeSpan DelayForAttempt(int attempt)
    {
        return TimeSpan.FromSeconds(BaseDelay.TotalSeconds * Math.Pow(2, attempt - 1));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var coordinator in _coordinators)
        {
            coordinator.Dispose();
        }

        Database.Dispose();
        TryDeleteDirectory(_directory);
        GC.SuppressFinalize(this);
    }

    private async Task<ApiResultModel<CatchDto>> ServerUpsertAsync(CatchDto dto)
    {
        if (UpsertInFlight is not null)
        {
            await UpsertInFlight(dto);
        }

        if (UpsertRejection?.Invoke(dto) is { } rejection)
        {
            return ApiResultModel<CatchDto>.Failed(rejection);
        }

        if (dto.Photographs.Count == 0)
        {
            return ApiResultModel<CatchDto>.Failed(ApiOutcomeEnum.ValidationFailed);
        }

        ServerCatches[dto.Id] = dto;
        return UpsertLostResponse?.Invoke(dto) is { } lost
            ? ApiResultModel<CatchDto>.Failed(lost)
            : ApiResultModel<CatchDto>.Succeeded(dto);
    }

    private ApiResultModel<NoContentModel> ServerDeletePhotograph(Guid catchId, Guid photographId)
    {
        if (DeleteRejection?.Invoke(catchId, photographId) is { } rejection)
        {
            return ApiResultModel<NoContentModel>.Failed(rejection);
        }

        if (!ServerCatches.TryGetValue(catchId, out var existing)
            || existing.Photographs.All(photograph => photograph.Id != photographId))
        {
            return ApiResultModel<NoContentModel>.Failed(ApiOutcomeEnum.NotFound);
        }

        ServerCatches[catchId] = existing with
        {
            Photographs = existing.Photographs.Where(photograph => photograph.Id != photographId).ToArray()
        };
        return ApiResultModel<NoContentModel>.Succeeded(NoContentModel.Instance);
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

    protected sealed record OutboxRow(
        Guid Id,
        Guid UserId,
        OutboxAggregateTypeEnum AggregateType,
        Guid AggregateId,
        OutboxOperationTypeEnum OperationType,
        OutboxStatusEnum Status,
        int AttemptCount,
        DateTimeOffset AvailableAt,
        string? LastError,
        string PayloadJson,
        string? LeaseOwner);

    /// <summary>A controllable clock. Timers are inert so the retry timer never starts a competing run.</summary>
    protected sealed class AdjustableClock : TimeProvider
    {
        private DateTimeOffset _now;

        public AdjustableClock(DateTimeOffset now)
        {
            _now = now;
        }

        public void Advance(TimeSpan by)
        {
            _now += by;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            return Substitute.For<ITimer>();
        }
    }
}
