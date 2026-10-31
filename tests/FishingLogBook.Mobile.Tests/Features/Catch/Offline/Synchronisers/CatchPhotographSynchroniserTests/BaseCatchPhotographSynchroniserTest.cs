using System.Text;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Common.Offline.Coordinators;
using FishingLogBook.Mobile.Common.Offline.Enums;
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
using FishingLogBook.Mobile.Features.Photographs.Clients;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Shared.Dtos;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Offline.Synchronisers.CatchPhotographSynchroniserTests;

public abstract class BaseCatchPhotographSynchroniserTest : IDisposable
{
    protected const int MaximumAttempts = 3;

    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected readonly IAccountSessionCoordinator MockSessionCoordinator =
        Substitute.For<IAccountSessionCoordinator>();
    protected readonly IConnectivityService MockConnectivity = Substitute.For<IConnectivityService>();
    protected readonly ISessionLifecycleCoordinator MockLifecycle = Substitute.For<ISessionLifecycleCoordinator>();
    protected readonly IOutboxChangeNotifier MockNotifier = Substitute.For<IOutboxChangeNotifier>();
    protected readonly TimeProvider MockClock = Substitute.For<TimeProvider>();
    protected readonly ICatchSyncClient MockCatchSyncClient = Substitute.For<ICatchSyncClient>();
    protected readonly IPhotographUploadClient MockUploadClient = Substitute.For<IPhotographUploadClient>();
    protected readonly IPhotographFileStore MockFileStore = Substitute.For<IPhotographFileStore>();
    protected readonly IFileSystemAdapter MockFileSystem = Substitute.For<IFileSystemAdapter>();

    protected readonly SqliteConnectionFactory ConnectionFactory;
    protected readonly SqliteDatabase Database;
    protected readonly SqliteOutboxStore OutboxStore;
    protected readonly SqliteCatchStore CatchStore;
    protected readonly SqliteCatchSyncStateStore StateStore;
    protected readonly SqliteCatchSyncStatusStore StatusStore;
    protected readonly CatchMetadataSynchroniser MetadataSynchroniser;
    protected readonly CatchPhotographSynchroniser Sut;
    protected readonly OutboxSyncCoordinator Coordinator;

    /// <summary>Server side: catch id -> photograph rows (the id set replaced by every metadata upsert).</summary>
    protected readonly Dictionary<Guid, Dictionary<Guid, string>> ServerCatches = new();

    /// <summary>Object storage: object key -> uploaded bytes.</summary>
    protected readonly Dictionary<string, byte[]> ObjectStore = new();

    /// <summary>Number of times the server accepted a Record call, per photograph.</summary>
    protected readonly Dictionary<Guid, int> ServerRecordCounts = new();

    /// <summary>Upload URLs issued per photograph, in issue order.</summary>
    protected readonly Dictionary<Guid, List<string>> IssuedUrls = new();

    /// <summary>Ordered log of every client interaction ("create:id", "put:id", "record:id", "delete:id", "upsert").</summary>
    protected readonly List<string> CallLog = [];

    protected readonly Dictionary<Guid, Queue<ApiOutcomeEnum>> CreateUrlFaults = new();
    protected readonly Dictionary<Guid, Queue<PhotographUploadOutcomeEnum>> UploadFaults = new();
    protected readonly Dictionary<Guid, PhotographUploadOutcomeEnum> PermanentUploadFaults = new();
    protected readonly Dictionary<Guid, Queue<RecordFaultModel>> RecordFaults = new();
    protected readonly Dictionary<Guid, byte[]> UploadedBytesByPhotograph = new();

    protected readonly Dictionary<string, byte[]> LocalFiles = new();
    protected readonly HashSet<string> MissingFiles = [];

    protected Func<Guid, Task>? OnUpload;
    protected bool GoOfflineAfterUpsert;
    protected bool Online = true;
    protected DateTimeOffset Now = DateTimeOffset.Parse("2026-06-01T10:00:00Z");

    private readonly string _directory;
    private readonly List<OutboxSyncCoordinator> _coordinators = [];
    private int _urlCounter;
    private bool _disposed;

    protected BaseCatchPhotographSynchroniserTest()
    {
        _directory = Path.Combine(Path.GetTempPath(), "flb-photo-sync-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        var lease = new AccountSessionLeaseModel(OwnerUserId, 1, CancellationToken.None);
        MockSessionCoordinator.Capture().Returns(lease);
        MockSessionCoordinator.IsCurrent(Arg.Any<AccountSessionLeaseModel>()).Returns(true);
        MockConnectivity.IsOnline.Returns(_ => Online);
        MockClock.GetUtcNow().Returns(_ => Now = Now.AddMilliseconds(1));
        MockClock.LocalTimeZone.Returns(TimeZoneInfo.Utc);

        ConnectionFactory = new SqliteConnectionFactory(Path.Combine(_directory, "local-logbook.db"));
        var migrator = new SqliteMigrator(ConnectionFactory, NullLogger<SqliteMigrator>.Instance);
        Database = new SqliteDatabase(ConnectionFactory, migrator, NullLogger<SqliteDatabase>.Instance);
        OutboxStore = new SqliteOutboxStore(ConnectionFactory, Database);
        CatchStore = new SqliteCatchStore(
            MockSessionCoordinator,
            ConnectionFactory,
            Database,
            OutboxStore,
            MockNotifier,
            MockClock,
            NullLogger<SqliteCatchStore>.Instance);
        StateStore = new SqliteCatchSyncStateStore(ConnectionFactory, Database);
        StatusStore = new SqliteCatchSyncStatusStore(ConnectionFactory, Database);

        ConfigureFileSystem();
        ConfigureServer();
        ConfigureObjectStorage();

        MetadataSynchroniser = new CatchMetadataSynchroniser(CatchStore, MockCatchSyncClient, StateStore, OutboxStore, MockClock);
        Sut = new CatchPhotographSynchroniser(
            CatchStore,
            MockCatchSyncClient,
            MockUploadClient,
            StateStore,
            MockFileStore,
            MockFileSystem,
            OutboxStore,
            Database,
            MockClock);
        Coordinator = CreateCoordinator();
    }

    protected OutboxSyncCoordinator CreateCoordinator()
    {
        var coordinator = new OutboxSyncCoordinator(
            MockSessionCoordinator,
            MockConnectivity,
            MockLifecycle,
            OutboxStore,
            Database,
            [MetadataSynchroniser, Sut],
            MockNotifier,
            new OutboxBackoffPolicy(TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(15), MaximumAttempts, () => 0.5),
            MockClock,
            NullLogger<OutboxSyncCoordinator>.Instance);
        _coordinators.Add(coordinator);
        return coordinator;
    }

    protected Task RunAsync(OutboxSyncCoordinator? coordinator = null)
    {
        return (coordinator ?? Coordinator).RunAsync(OutboxSyncTriggerEnum.Manual, CancellationToken.None);
    }

    protected void Advance(TimeSpan span)
    {
        Now += span;
    }

    protected static string ObjectKeyFor(Guid catchId, Guid photographId)
    {
        return $"catches/{catchId:D}/{photographId:D}";
    }

    protected PhotoModel NewPhoto(int sortOrder, string contentType = "image/jpeg")
    {
        var id = Guid.NewGuid();
        var relative = $"photos/{id:D}.bin";
        var absolute = "/files/" + relative;
        var bytes = Encoding.UTF8.GetBytes($"photo-{id:D}-{sortOrder}")
            .Concat(id.ToByteArray())
            .ToArray();
        LocalFiles[absolute] = bytes;
        return new PhotoModel(id, sortOrder, contentType, relative, absolute, bytes);
    }

    protected async Task<TestCatchModel> SaveCatchAsync(int photographCount)
    {
        var photographs = Enumerable.Range(0, photographCount)
            .Select(index => NewPhoto(index, index % 2 == 0 ? "image/jpeg" : "image/png"))
            .ToList();
        var catchModel = new TestCatchModel(Guid.NewGuid());
        var result = await ResaveCatchAsync(catchModel, photographs);
        Assert.True(result.IsSuccess);
        return catchModel;
    }

    protected Task<LocalCatchSaveResultModel> ResaveCatchAsync(
        TestCatchModel catchModel,
        IReadOnlyList<PhotoModel> photographs,
        LocalCatchLifecycleEnum lifecycle = LocalCatchLifecycleEnum.Saved)
    {
        catchModel.Photographs = photographs.ToList();
        return CatchStore.SaveAsync(
            new LocalCatchSaveArgs
            {
                Id = catchModel.Id,
                CaughtOn = DateTimeOffset.Parse("2026-05-01T12:00:00Z"),
                SpeciesName = "Brown trout",
                Lifecycle = lifecycle,
                Photographs = photographs
                    .Select((photo, index) => new LocalCatchPhotographSaveArgs
                    {
                        Id = photo.Id,
                        SortOrder = index,
                        LocalFilePath = photo.RelativePath,
                        ContentType = photo.ContentType
                    })
                    .ToList()
            },
            CancellationToken.None);
    }

    /// <summary>Runs only the metadata operation, leaving every photograph operation pending.</summary>
    protected async Task SyncMetadataOnlyAsync()
    {
        GoOfflineAfterUpsert = true;
        await RunAsync();
        GoOfflineAfterUpsert = false;
        Online = true;
    }

    protected void EnqueueCreateUrlFault(Guid photographId, params ApiOutcomeEnum[] outcomes)
    {
        CreateUrlFaults[photographId] = new Queue<ApiOutcomeEnum>(outcomes);
    }

    protected void EnqueueUploadFault(Guid photographId, params PhotographUploadOutcomeEnum[] outcomes)
    {
        UploadFaults[photographId] = new Queue<PhotographUploadOutcomeEnum>(outcomes);
    }

    protected void EnqueueRecordFault(Guid photographId, params RecordFaultModel[] faults)
    {
        RecordFaults[photographId] = new Queue<RecordFaultModel>(faults);
    }

    protected IEnumerable<string> CallsFor(Guid photographId)
    {
        return CallLog.Where(entry => entry.EndsWith(":" + photographId.ToString("D"), StringComparison.Ordinal));
    }

    protected int CountCalls(string prefix, Guid photographId)
    {
        return CallLog.Count(entry => entry == $"{prefix}:{photographId:D}");
    }

    protected async Task<IReadOnlyList<OperationRow>> ReadOperationsAsync(OutboxOperationTypeEnum type)
    {
        await using var connection = await ConnectionFactory.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT aggregateid, status, attemptcount, lasterror, availableat, leaseowner
            FROM outboxoperations
            WHERE operationtype = $type
            ORDER BY createdon ASC, id ASC;
            """;
        command.Parameters.AddWithValue("$type", type.ToString());
        var rows = new List<OperationRow>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new OperationRow(
                Guid.Parse(reader.GetString(0)),
                Enum.Parse<OutboxStatusEnum>(reader.GetString(1)),
                reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                DateTimeOffset.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return rows;
    }

    protected async Task<IReadOnlyList<OperationRow>> ReadPhotographOperationsAsync(Guid photographId)
    {
        var rows = await ReadOperationsAsync(OutboxOperationTypeEnum.UploadCatchPhotograph);
        return rows.Where(row => row.AggregateId == photographId).ToList();
    }

    protected async Task<PhotographSyncStateModel?> ReadPhotographStateAsync(Guid photographId)
    {
        return await StateStore.GetPhotographStateAsync(OwnerUserId, photographId, CancellationToken.None);
    }

    protected async Task<CatchSyncStatusModel> ReadStatusAsync(Guid catchId)
    {
        var statuses = await StatusStore.ListAsync(OwnerUserId, CancellationToken.None);
        return statuses[catchId];
    }

    protected async Task<long> ScalarAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await ConnectionFactory.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return Convert.ToInt64(await command.ExecuteScalarAsync());
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

    protected Task<long> CountLocalPhotographRowsAsync(Guid catchId)
    {
        return ScalarAsync(
            "SELECT COUNT(*) FROM localcatchphotographs WHERE catchid = $catchId;",
            ("$catchId", catchId.ToString("D")));
    }

    protected Task<long> SyncedRevisionAsync(Guid catchId)
    {
        return StateStore.GetSyncedRevisionAsync(OwnerUserId, catchId, CancellationToken.None);
    }

    protected async Task<long> LocalRevisionAsync(Guid catchId)
    {
        return (await CatchStore.GetForOwnerAsync(OwnerUserId, catchId, CancellationToken.None))!.Revision;
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
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(_directory))
                {
                    Directory.Delete(_directory, recursive: true);
                }

                break;
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

        GC.SuppressFinalize(this);
    }

    private void ConfigureFileSystem()
    {
        MockFileStore.TryResolveRelativePath(Arg.Any<Guid>(), Arg.Any<string>(), out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[2] = "/files/" + callInfo.ArgAt<string>(1);
                return true;
            });
        MockFileSystem.FileExists(Arg.Any<string>())
            .Returns(callInfo => LocalFiles.ContainsKey(callInfo.ArgAt<string>(0))
                && !MissingFiles.Contains(callInfo.ArgAt<string>(0)));
        MockFileSystem.OpenRead(Arg.Any<string>())
            .Returns(callInfo => (Stream)new MemoryStream(LocalFiles[callInfo.ArgAt<string>(0)]));
    }

    private void ConfigureServer()
    {
        MockCatchSyncClient.UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var dto = callInfo.ArgAt<CatchDto>(0);
                CallLog.Add("upsert");
                if (dto.Photographs.Count == 0)
                {
                    return Task.FromResult(ApiResultModel<CatchDto>.Failed(ApiOutcomeEnum.ValidationFailed));
                }

                ServerCatches[dto.Id] = dto.Photographs.ToDictionary(photo => photo.Id, photo => photo.ContentType);
                if (GoOfflineAfterUpsert)
                {
                    Online = false;
                }

                return Task.FromResult(ApiResultModel<CatchDto>.Succeeded(dto));
            });

        MockCatchSyncClient.CreatePhotographUploadAsync(
                Arg.Any<Guid>(),
                Arg.Any<PhotographUploadRequestDto>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var catchId = callInfo.ArgAt<Guid>(0);
                var request = callInfo.ArgAt<PhotographUploadRequestDto>(1);
                CallLog.Add($"create:{request.PhotographId:D}");
                if (CreateUrlFaults.TryGetValue(request.PhotographId, out var faults) && faults.Count > 0)
                {
                    return Task.FromResult(
                        ApiResultModel<PhotographUploadDto>.Failed(faults.Dequeue()));
                }

                if (!ServerCatches.TryGetValue(catchId, out var rows)
                    || !rows.TryGetValue(request.PhotographId, out var contentType)
                    || contentType != request.ContentType)
                {
                    return Task.FromResult(ApiResultModel<PhotographUploadDto>.Failed(ApiOutcomeEnum.NotFound));
                }

                var key = ObjectKeyFor(catchId, request.PhotographId);
                var url = $"https://storage.example/{key}?sig={++_urlCounter}";
                if (!IssuedUrls.TryGetValue(request.PhotographId, out var urls))
                {
                    urls = [];
                    IssuedUrls[request.PhotographId] = urls;
                }

                urls.Add(url);
                return Task.FromResult(ApiResultModel<PhotographUploadDto>.Succeeded(new PhotographUploadDto(key, url)));
            });

        MockCatchSyncClient.RecordPhotographAsync(
                Arg.Any<Guid>(),
                Arg.Any<RecordPhotographDto>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var catchId = callInfo.ArgAt<Guid>(0);
                var request = callInfo.ArgAt<RecordPhotographDto>(1);
                CallLog.Add($"record:{request.PhotographId:D}");
                RecordFaultModel? fault = null;
                if (RecordFaults.TryGetValue(request.PhotographId, out var faults) && faults.Count > 0)
                {
                    fault = faults.Dequeue();
                    if (!fault.ServerRecords)
                    {
                        return Task.FromResult(ApiResultModel<NoContentModel>.Failed(fault.Outcome));
                    }
                }

                if (!ServerCatches.TryGetValue(catchId, out var rows)
                    || !rows.ContainsKey(request.PhotographId))
                {
                    return Task.FromResult(ApiResultModel<NoContentModel>.Failed(ApiOutcomeEnum.NotFound));
                }

                if (request.ObjectKey != ObjectKeyFor(catchId, request.PhotographId)
                    || request.ContentType != rows[request.PhotographId])
                {
                    return Task.FromResult(ApiResultModel<NoContentModel>.Failed(ApiOutcomeEnum.ValidationFailed));
                }

                ServerRecordCounts[request.PhotographId] = ServerRecordCounts.GetValueOrDefault(request.PhotographId) + 1;
                return Task.FromResult(fault is null
                    ? ApiResultModel<NoContentModel>.Succeeded(NoContentModel.Instance)
                    : ApiResultModel<NoContentModel>.Failed(fault.Outcome));
            });

        MockCatchSyncClient.DeletePhotographAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var catchId = callInfo.ArgAt<Guid>(0);
                var photographId = callInfo.ArgAt<Guid>(1);
                CallLog.Add($"delete:{photographId:D}");
                ObjectStore.Remove(ObjectKeyFor(catchId, photographId));
                var removed = ServerCatches.TryGetValue(catchId, out var rows) && rows.Remove(photographId);
                return Task.FromResult(removed
                    ? ApiResultModel<NoContentModel>.Succeeded(NoContentModel.Instance)
                    : ApiResultModel<NoContentModel>.Failed(ApiOutcomeEnum.NotFound));
            });
    }

    private void ConfigureObjectStorage()
    {
        MockUploadClient.UploadAsync(
                Arg.Any<string>(),
                Arg.Any<Stream>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => UploadAsync(callInfo.ArgAt<string>(0), callInfo.ArgAt<Stream>(1)));
    }

    private async Task<PhotographUploadOutcomeEnum> UploadAsync(string url, Stream content)
    {
        var uri = new Uri(url);
        var key = uri.AbsolutePath.TrimStart('/');
        var photographId = Guid.Parse(key.Split('/')[^1]);
        CallLog.Add($"put:{photographId:D}");
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer);
        if (OnUpload is not null)
        {
            await OnUpload(photographId);
        }

        if (PermanentUploadFaults.TryGetValue(photographId, out var permanent))
        {
            return permanent;
        }

        if (UploadFaults.TryGetValue(photographId, out var faults) && faults.Count > 0)
        {
            var outcome = faults.Dequeue();
            if (outcome != PhotographUploadOutcomeEnum.Uploaded)
            {
                return outcome;
            }
        }

        ObjectStore[key] = buffer.ToArray();
        UploadedBytesByPhotograph[photographId] = buffer.ToArray();
        return PhotographUploadOutcomeEnum.Uploaded;
    }

    protected sealed record PhotoModel(
        Guid Id,
        int SortOrder,
        string ContentType,
        string RelativePath,
        string AbsolutePath,
        byte[] Bytes);

    protected sealed class TestCatchModel(Guid id)
    {
        public Guid Id { get; } = id;

        public List<PhotoModel> Photographs { get; set; } = [];
    }

    protected sealed record RecordFaultModel(ApiOutcomeEnum Outcome, bool ServerRecords);

    protected sealed record OperationRow(
        Guid AggregateId,
        OutboxStatusEnum Status,
        int AttemptCount,
        string? LastError,
        DateTimeOffset AvailableAt,
        string? LeaseOwner);
}
