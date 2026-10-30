using System.Text.Json;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Persistence.Enums;
using FishingLogBook.Mobile.Features.Catch.Persistence.Models;
using FishingLogBook.Mobile.Features.Catch.Persistence.Stores;
using FishingLogBook.Mobile.Features.LocalPersistence.Enums;
using FishingLogBook.Mobile.Features.LocalPersistence.Models;
using FishingLogBook.Mobile.Features.LocalPersistence.Sqlite;
using FishingLogBook.Mobile.Features.LocalPersistence.Stores;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Catch.Persistence.Services;

public sealed class LocalCatchService : ILocalCatchService
{
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IAccountSessionCoordinator _sessionCoordinator;
    private readonly ISqliteDatabase _database;
    private readonly ILocalCatchStore _catchStore;
    private readonly IOutboxStore _outboxStore;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LocalCatchService> _logger;

    public LocalCatchService(
        IAccountSessionCoordinator sessionCoordinator,
        ISqliteDatabase database,
        ILocalCatchStore catchStore,
        IOutboxStore outboxStore,
        TimeProvider timeProvider,
        ILogger<LocalCatchService> logger)
    {
        ArgumentNullException.ThrowIfNull(sessionCoordinator);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(catchStore);
        ArgumentNullException.ThrowIfNull(outboxStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _sessionCoordinator = sessionCoordinator;
        _database = database;
        _catchStore = catchStore;
        _outboxStore = outboxStore;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<LocalCatchSaveResultModel> SaveAsync(
        LocalCatchSaveArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Id == Guid.Empty)
        {
            return LocalCatchSaveResultModel.Failed(LocalCatchSaveOutcomeEnum.InvalidRequest);
        }

        var lease = _sessionCoordinator.Capture();
        if (lease is null)
        {
            return LocalCatchSaveResultModel.Failed(LocalCatchSaveOutcomeEnum.NotSignedIn);
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lease.CancellationToken);
        var now = _timeProvider.GetUtcNow();
        LocalCatchSaveResultModel? result = null;

        try
        {
            await _database.ExecuteInTransactionAsync(
                async (connection, transaction, ct) =>
                {
                    result = await PersistAsync(connection, transaction, lease, args, now, ct);
                },
                linkedCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException exception) when (IsOwnershipFailure(exception))
        {
            _logger.LogWarning(exception, "Local catch save rejected for catch {CatchId}.", args.Id);
            return LocalCatchSaveResultModel.Failed(LocalCatchSaveOutcomeEnum.Forbidden);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Failed to save local catch {CatchId}.", args.Id);
            return LocalCatchSaveResultModel.Failed(LocalCatchSaveOutcomeEnum.StorageFailed);
        }

        return result ?? LocalCatchSaveResultModel.Failed(LocalCatchSaveOutcomeEnum.StorageFailed);
    }

    private static bool IsOwnershipFailure(InvalidOperationException exception)
    {
        return exception.Message.Contains("different account", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("ownership", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("current account", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<LocalCatchRecordModel?> GetAsync(Guid catchId, CancellationToken cancellationToken)
    {
        var lease = _sessionCoordinator.Capture();
        if (lease is null)
        {
            return null;
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lease.CancellationToken);
        var record = await _catchStore.GetAsync(lease.UserId, catchId, linkedCancellation.Token);
        if (record is null || !_sessionCoordinator.IsCurrent(lease))
        {
            return null;
        }

        return record;
    }

    public async Task<IReadOnlyList<LocalCatchRecordModel>> ListAsync(CancellationToken cancellationToken)
    {
        var lease = _sessionCoordinator.Capture();
        if (lease is null)
        {
            return Array.Empty<LocalCatchRecordModel>();
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lease.CancellationToken);
        var records = await _catchStore.ListAsync(lease.UserId, linkedCancellation.Token);
        if (!_sessionCoordinator.IsCurrent(lease))
        {
            return Array.Empty<LocalCatchRecordModel>();
        }

        return records;
    }

    private async Task<LocalCatchSaveResultModel> PersistAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        AccountSessionLeaseModel lease,
        LocalCatchSaveArgs args,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        EnsureCurrentSession(lease, "before the save");
        var recordedByUserId = args.RecordedByUserId ?? lease.UserId;
        var caughtByUserId = args.CaughtByUserId ?? lease.UserId;
        if (recordedByUserId != lease.UserId)
        {
            return LocalCatchSaveResultModel.Failed(LocalCatchSaveOutcomeEnum.Forbidden);
        }

        await UpsertTripIfNeededAsync(connection, transaction, lease.UserId, args, now, cancellationToken);

        var existing = await _catchStore.GetRevisionStateAsync(
            connection,
            transaction,
            lease.UserId,
            args.Id,
            cancellationToken);
        var revision = existing is null ? 1L : existing.Revision + 1L;
        var catchRecord = BuildCatchRecord(
            lease.UserId,
            caughtByUserId,
            recordedByUserId,
            args,
            revision,
            existing?.CreatedOn ?? now,
            now);

        await _catchStore.UpsertCatchAsync(connection, transaction, catchRecord, cancellationToken);
        await _catchStore.ReplacePhotographsAsync(
            connection,
            transaction,
            lease.UserId,
            args.Id,
            catchRecord.Photographs,
            cancellationToken);
        await _outboxStore.UpsertPendingAsync(
            connection,
            transaction,
            BuildPendingOutbox(lease.UserId, catchRecord, now),
            cancellationToken);
        EnsureCurrentSession(lease, "during the save");

        return LocalCatchSaveResultModel.Saved(args.Id, revision);
    }

    private void EnsureCurrentSession(AccountSessionLeaseModel lease, string phase)
    {
        if (!_sessionCoordinator.IsCurrent(lease))
        {
            throw new OperationCanceledException($"The account session changed {phase}.");
        }
    }

    private async Task UpsertTripIfNeededAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid userId,
        LocalCatchSaveArgs args,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (args.TripId is not Guid tripId)
        {
            return;
        }

        await _catchStore.UpsertTripStubAsync(
            connection,
            transaction,
            new LocalTripReferenceModel
            {
                Id = tripId,
                UserId = userId,
                Title = args.TripTitle,
                UpdatedOn = now
            },
            cancellationToken);
    }

    private static LocalCatchRecordModel BuildCatchRecord(
        Guid ownerUserId,
        Guid caughtByUserId,
        Guid recordedByUserId,
        LocalCatchSaveArgs args,
        long revision,
        DateTimeOffset createdOn,
        DateTimeOffset updatedOn)
    {
        var photographs = args.Photographs
            .Select(photograph => new LocalCatchPhotographModel
            {
                Id = photograph.Id,
                CatchId = args.Id,
                UserId = ownerUserId,
                SortOrder = photograph.SortOrder,
                LocalFilePath = photograph.LocalFilePath,
                ContentType = photograph.ContentType,
                CreatedOn = updatedOn
            })
            .ToArray();

        return new LocalCatchRecordModel
        {
            Id = args.Id,
            UserId = ownerUserId,
            TripId = args.TripId,
            CaughtByUserId = caughtByUserId,
            RecordedByUserId = recordedByUserId,
            CaughtOn = args.CaughtOn,
            SpeciesName = args.SpeciesName,
            Weight = args.Weight,
            Length = args.Length,
            Method = args.Method,
            BaitOrLure = args.BaitOrLure,
            Notes = args.Notes,
            PlaceName = args.PlaceName,
            Location = args.Location,
            Revision = revision,
            Lifecycle = args.Lifecycle,
            CreatedOn = createdOn,
            UpdatedOn = updatedOn,
            Photographs = photographs
        };
    }

    private static OutboxOperationModel BuildPendingOutbox(
        Guid userId,
        LocalCatchRecordModel catchRecord,
        DateTimeOffset now)
    {
        var payloadJson = JsonSerializer.Serialize(
            new CatchOutboxPayload(
                catchRecord.Id,
                catchRecord.UserId,
                catchRecord.TripId,
                catchRecord.CaughtByUserId,
                catchRecord.RecordedByUserId,
                catchRecord.CaughtOn,
                catchRecord.SpeciesName,
                catchRecord.Weight,
                catchRecord.Length,
                catchRecord.Method,
                catchRecord.BaitOrLure,
                catchRecord.Notes,
                catchRecord.PlaceName,
                catchRecord.Revision,
                catchRecord.Lifecycle.ToString()),
            PayloadSerializerOptions);

        return new OutboxOperationModel
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AggregateType = OutboxAggregateTypeEnum.Catch,
            AggregateId = catchRecord.Id,
            OperationType = OutboxOperationTypeEnum.UpsertCatchMetadata,
            PayloadJson = payloadJson,
            Status = OutboxStatusEnum.Pending,
            AttemptCount = 0,
            CreatedOn = now,
            AvailableAt = now
        };
    }

    private sealed record CatchOutboxPayload(
        Guid CatchId,
        Guid UserId,
        Guid? TripId,
        Guid CaughtByUserId,
        Guid RecordedByUserId,
        DateTimeOffset CaughtOn,
        string? SpeciesName,
        double? Weight,
        double? Length,
        string? Method,
        string? BaitOrLure,
        string? Notes,
        string? PlaceName,
        long Revision,
        string Lifecycle);
}
