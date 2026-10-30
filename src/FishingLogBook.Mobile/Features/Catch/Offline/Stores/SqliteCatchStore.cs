using System.Text.Json;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Models;
using FishingLogBook.Mobile.Common.Offline.Sqlite;
using FishingLogBook.Mobile.Common.Offline.Stores;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Catch.Offline.Stores;

public sealed class SqliteCatchStore : ICatchStore
{
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IAccountSessionCoordinator _sessionCoordinator;
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly ISqliteDatabase _database;
    private readonly IOutboxStore _outboxStore;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SqliteCatchStore> _logger;

    public SqliteCatchStore(
        IAccountSessionCoordinator sessionCoordinator,
        ISqliteConnectionFactory connectionFactory,
        ISqliteDatabase database,
        IOutboxStore outboxStore,
        TimeProvider timeProvider,
        ILogger<SqliteCatchStore> logger)
    {
        ArgumentNullException.ThrowIfNull(sessionCoordinator);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(outboxStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _sessionCoordinator = sessionCoordinator;
        _connectionFactory = connectionFactory;
        _database = database;
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
        var record = await GetForOwnerAsync(lease.UserId, catchId, linkedCancellation.Token);
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
        var records = await ListForOwnerAsync(lease.UserId, linkedCancellation.Token);
        if (!_sessionCoordinator.IsCurrent(lease))
        {
            return Array.Empty<LocalCatchRecordModel>();
        }

        return records;
    }

    public async Task UpsertTripStubAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        LocalTripReferenceModel trip,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(trip);

        await using var ownershipCommand = connection.CreateCommand();
        ownershipCommand.Transaction = transaction;
        ownershipCommand.CommandText =
            """
            SELECT userid
            FROM localtrips
            WHERE id = $id
            LIMIT 1;
            """;
        ownershipCommand.Parameters.AddWithValue("$id", trip.Id.ToString("D"));
        var existingOwner = await ownershipCommand.ExecuteScalarAsync(cancellationToken) as string;
        if (existingOwner is not null && !string.Equals(existingOwner, trip.UserId.ToString("D"), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The trip belongs to a different account.");
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO localtrips (id, userid, title, updatedon)
            VALUES ($id, $userId, $title, $updatedOn)
            ON CONFLICT(id) DO UPDATE SET
                title = COALESCE(excluded.title, localtrips.title),
                updatedon = excluded.updatedon
            WHERE localtrips.userid = excluded.userid;
            """;
        command.Parameters.AddWithValue("$id", trip.Id.ToString("D"));
        command.Parameters.AddWithValue("$userId", trip.UserId.ToString("D"));
        command.Parameters.AddWithValue("$title", (object?)trip.Title ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedOn", SqliteDateTime.Format(trip.UpdatedOn));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<LocalCatchRevisionStateModel?> GetRevisionStateAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid userId,
        Guid catchId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT revision, createdon
            FROM localcatches
            WHERE id = $id
              AND userid = $userId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", catchId.ToString("D"));
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new LocalCatchRevisionStateModel
        {
            Revision = reader.GetInt64(0),
            CreatedOn = SqliteDateTime.Parse(reader.GetString(1))
        };
    }

    public async Task UpsertCatchAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        LocalCatchRecordModel catchRecord,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(catchRecord);

        await using var foreignCommand = connection.CreateCommand();
        foreignCommand.Transaction = transaction;
        foreignCommand.CommandText =
            """
            SELECT userid
            FROM localcatches
            WHERE id = $id
            LIMIT 1;
            """;
        foreignCommand.Parameters.AddWithValue("$id", catchRecord.Id.ToString("D"));
        var existingOwner = await foreignCommand.ExecuteScalarAsync(cancellationToken) as string;
        if (existingOwner is not null
            && !string.Equals(existingOwner, catchRecord.UserId.ToString("D"), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The catch belongs to a different account.");
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO localcatches (
                id, userid, tripid, caughtbyuserid, recordedbyuserid, caughton,
                speciesname, weight, length, method, baitorlure, notes,
                placename, latitude, longitude, locationaccuracymetres, locationcapturedon,
                locationsource, locationvisibility, locationconsentversion, revision,
                lifecycle, createdon, updatedon)
            VALUES (
                $id, $userId, $tripId, $caughtByUserId, $recordedByUserId, $caughtOn,
                $speciesName, $weight, $length, $method, $baitOrLure, $notes,
                $placeName, $latitude, $longitude, $locationAccuracyMetres, $locationCapturedOn,
                $locationSource, $locationVisibility, $locationConsentVersion, $revision,
                $lifecycle, $createdOn, $updatedOn)
            ON CONFLICT(id) DO UPDATE SET
                tripid = excluded.tripid,
                caughtbyuserid = excluded.caughtbyuserid,
                recordedbyuserid = excluded.recordedbyuserid,
                caughton = excluded.caughton,
                speciesname = excluded.speciesname,
                weight = excluded.weight,
                length = excluded.length,
                method = excluded.method,
                baitorlure = excluded.baitorlure,
                notes = excluded.notes,
                placename = excluded.placename,
                latitude = excluded.latitude,
                longitude = excluded.longitude,
                locationaccuracymetres = excluded.locationaccuracymetres,
                locationcapturedon = excluded.locationcapturedon,
                locationsource = excluded.locationsource,
                locationvisibility = excluded.locationvisibility,
                locationconsentversion = excluded.locationconsentversion,
                revision = excluded.revision,
                lifecycle = excluded.lifecycle,
                updatedon = excluded.updatedon
            WHERE localcatches.userid = excluded.userid;
            """;
        BindCatchParameters(command, catchRecord);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected != 1)
        {
            throw new InvalidOperationException("Failed to upsert the local catch for the current account.");
        }
    }

    public async Task ReplacePhotographsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid userId,
        Guid catchId,
        IReadOnlyList<LocalCatchPhotographModel> photographs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(photographs);

        await using var deleteCommand = connection.CreateCommand();
        deleteCommand.Transaction = transaction;
        deleteCommand.CommandText =
            """
            DELETE FROM localcatchphotographs
            WHERE catchid = $catchId
              AND userid = $userId;
            """;
        deleteCommand.Parameters.AddWithValue("$catchId", catchId.ToString("D"));
        deleteCommand.Parameters.AddWithValue("$userId", userId.ToString("D"));
        await deleteCommand.ExecuteNonQueryAsync(cancellationToken);

        foreach (var photograph in photographs)
        {
            if (photograph.CatchId != catchId || photograph.UserId != userId)
            {
                throw new InvalidOperationException("Photograph ownership does not match the catch.");
            }

            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
                """
                INSERT INTO localcatchphotographs (
                    id, catchid, userid, sortorder, localfilepath, contenttype, createdon)
                VALUES (
                    $id, $catchId, $userId, $sortOrder, $localFilePath, $contentType, $createdOn);
                """;
            insertCommand.Parameters.AddWithValue("$id", photograph.Id.ToString("D"));
            insertCommand.Parameters.AddWithValue("$catchId", photograph.CatchId.ToString("D"));
            insertCommand.Parameters.AddWithValue("$userId", photograph.UserId.ToString("D"));
            insertCommand.Parameters.AddWithValue("$sortOrder", photograph.SortOrder);
            insertCommand.Parameters.AddWithValue("$localFilePath", (object?)photograph.LocalFilePath ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("$contentType", (object?)photograph.ContentType ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("$createdOn", SqliteDateTime.Format(photograph.CreatedOn));
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public Task<LocalCatchRecordModel?> GetForOwnerAsync(
        Guid userId,
        Guid catchId,
        CancellationToken cancellationToken)
    {
        return ReadCatchAsync(userId, catchId, cancellationToken);
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

        var existing = await GetRevisionStateAsync(
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

        await UpsertCatchAsync(connection, transaction, catchRecord, cancellationToken);
        await ReplacePhotographsAsync(
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

        await UpsertTripStubAsync(
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

    private static bool IsOwnershipFailure(InvalidOperationException exception)
    {
        return exception.Message.Contains("different account", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("ownership", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("current account", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<LocalCatchRecordModel?> ReadCatchAsync(
        Guid userId,
        Guid catchId,
        CancellationToken cancellationToken)
    {
        await _database.EnsureInitializedAsync(cancellationToken);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = CatchSelectSql + " WHERE c.id = $id AND c.userid = $userId LIMIT 1;";
        command.Parameters.AddWithValue("$id", catchId.ToString("D"));
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        var catches = await ReadCatchesAsync(connection, command, cancellationToken);
        return catches.Count == 0 ? null : catches[0];
    }

    private async Task<IReadOnlyList<LocalCatchRecordModel>> ListForOwnerAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _database.EnsureInitializedAsync(cancellationToken);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = CatchSelectSql + " WHERE c.userid = $userId ORDER BY c.caughton DESC, c.id ASC;";
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        return await ReadCatchesAsync(connection, command, cancellationToken);
    }

    private static void BindCatchParameters(SqliteCommand command, LocalCatchRecordModel catchRecord)
    {
        command.Parameters.AddWithValue("$id", catchRecord.Id.ToString("D"));
        command.Parameters.AddWithValue("$userId", catchRecord.UserId.ToString("D"));
        command.Parameters.AddWithValue(
            "$tripId",
            catchRecord.TripId is null ? DBNull.Value : catchRecord.TripId.Value.ToString("D"));
        command.Parameters.AddWithValue("$caughtByUserId", catchRecord.CaughtByUserId.ToString("D"));
        command.Parameters.AddWithValue("$recordedByUserId", catchRecord.RecordedByUserId.ToString("D"));
        command.Parameters.AddWithValue("$caughtOn", SqliteDateTime.Format(catchRecord.CaughtOn));
        command.Parameters.AddWithValue("$speciesName", (object?)catchRecord.SpeciesName ?? DBNull.Value);
        command.Parameters.AddWithValue("$weight", (object?)catchRecord.Weight ?? DBNull.Value);
        command.Parameters.AddWithValue("$length", (object?)catchRecord.Length ?? DBNull.Value);
        command.Parameters.AddWithValue("$method", (object?)catchRecord.Method ?? DBNull.Value);
        command.Parameters.AddWithValue("$baitOrLure", (object?)catchRecord.BaitOrLure ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)catchRecord.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$placeName", (object?)catchRecord.PlaceName ?? DBNull.Value);
        command.Parameters.AddWithValue("$latitude", (object?)catchRecord.Location?.Latitude ?? DBNull.Value);
        command.Parameters.AddWithValue("$longitude", (object?)catchRecord.Location?.Longitude ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$locationAccuracyMetres",
            (object?)catchRecord.Location?.AccuracyMetres ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$locationCapturedOn",
            catchRecord.Location is null
                ? DBNull.Value
                : SqliteDateTime.Format(catchRecord.Location.CapturedOn));
        command.Parameters.AddWithValue("$locationSource", (object?)catchRecord.Location?.Source ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$locationVisibility",
            (object?)catchRecord.Location?.Visibility ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$locationConsentVersion",
            (object?)catchRecord.Location?.ConsentVersion ?? DBNull.Value);
        command.Parameters.AddWithValue("$revision", catchRecord.Revision);
        command.Parameters.AddWithValue("$lifecycle", catchRecord.Lifecycle.ToString());
        command.Parameters.AddWithValue("$createdOn", SqliteDateTime.Format(catchRecord.CreatedOn));
        command.Parameters.AddWithValue("$updatedOn", SqliteDateTime.Format(catchRecord.UpdatedOn));
    }

    private const string CatchSelectSql =
        """
        SELECT
            c.id, c.userid, c.tripid, c.caughtbyuserid, c.recordedbyuserid, c.caughton,
            c.speciesname, c.weight, c.length, c.method, c.baitorlure, c.notes,
            c.placename, c.latitude, c.longitude, c.locationaccuracymetres, c.locationcapturedon,
            c.locationsource, c.locationvisibility, c.locationconsentversion, c.revision,
            c.lifecycle, c.createdon, c.updatedon
        FROM localcatches c
        """;

    private static async Task<IReadOnlyList<LocalCatchRecordModel>> ReadCatchesAsync(
        SqliteConnection connection,
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        var stubs = new List<LocalCatchRecordModel>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                stubs.Add(MapCatch(reader));
            }
        }

        var catches = new List<LocalCatchRecordModel>(stubs.Count);
        foreach (var catchRecord in stubs)
        {
            var photographs = await ReadPhotographsAsync(
                connection,
                catchRecord.UserId,
                catchRecord.Id,
                cancellationToken);
            catches.Add(new LocalCatchRecordModel
            {
                Id = catchRecord.Id,
                UserId = catchRecord.UserId,
                TripId = catchRecord.TripId,
                CaughtByUserId = catchRecord.CaughtByUserId,
                RecordedByUserId = catchRecord.RecordedByUserId,
                CaughtOn = catchRecord.CaughtOn,
                SpeciesName = catchRecord.SpeciesName,
                Weight = catchRecord.Weight,
                Length = catchRecord.Length,
                Method = catchRecord.Method,
                BaitOrLure = catchRecord.BaitOrLure,
                Notes = catchRecord.Notes,
                PlaceName = catchRecord.PlaceName,
                Location = catchRecord.Location,
                Revision = catchRecord.Revision,
                Lifecycle = catchRecord.Lifecycle,
                CreatedOn = catchRecord.CreatedOn,
                UpdatedOn = catchRecord.UpdatedOn,
                Photographs = photographs
            });
        }

        return catches;
    }

    private static LocalCatchRecordModel MapCatch(SqliteDataReader reader)
    {
        LocalCatchLocationModel? location = null;
        if (!reader.IsDBNull(13) && !reader.IsDBNull(14))
        {
            location = new LocalCatchLocationModel
            {
                Latitude = reader.GetDouble(13),
                Longitude = reader.GetDouble(14),
                AccuracyMetres = reader.IsDBNull(15) ? null : reader.GetDouble(15),
                CapturedOn = SqliteDateTime.Parse(reader.GetString(16)),
                Source = reader.GetString(17),
                Visibility = reader.GetString(18),
                ConsentVersion = reader.GetString(19)
            };
        }

        return new LocalCatchRecordModel
        {
            Id = Guid.Parse(reader.GetString(0)),
            UserId = Guid.Parse(reader.GetString(1)),
            TripId = reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)),
            CaughtByUserId = Guid.Parse(reader.GetString(3)),
            RecordedByUserId = Guid.Parse(reader.GetString(4)),
            CaughtOn = SqliteDateTime.Parse(reader.GetString(5)),
            SpeciesName = reader.IsDBNull(6) ? null : reader.GetString(6),
            Weight = reader.IsDBNull(7) ? null : reader.GetDouble(7),
            Length = reader.IsDBNull(8) ? null : reader.GetDouble(8),
            Method = reader.IsDBNull(9) ? null : reader.GetString(9),
            BaitOrLure = reader.IsDBNull(10) ? null : reader.GetString(10),
            Notes = reader.IsDBNull(11) ? null : reader.GetString(11),
            PlaceName = reader.IsDBNull(12) ? null : reader.GetString(12),
            Location = location,
            Revision = reader.GetInt64(20),
            Lifecycle = Enum.Parse<LocalCatchLifecycleEnum>(reader.GetString(21)),
            CreatedOn = SqliteDateTime.Parse(reader.GetString(22)),
            UpdatedOn = SqliteDateTime.Parse(reader.GetString(23)),
            Photographs = Array.Empty<LocalCatchPhotographModel>()
        };
    }

    private static async Task<IReadOnlyList<LocalCatchPhotographModel>> ReadPhotographsAsync(
        SqliteConnection connection,
        Guid userId,
        Guid catchId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, catchid, userid, sortorder, localfilepath, contenttype, createdon
            FROM localcatchphotographs
            WHERE catchid = $catchId
              AND userid = $userId
            ORDER BY sortorder ASC, id ASC;
            """;
        command.Parameters.AddWithValue("$catchId", catchId.ToString("D"));
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        var photographs = new List<LocalCatchPhotographModel>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            photographs.Add(new LocalCatchPhotographModel
            {
                Id = Guid.Parse(reader.GetString(0)),
                CatchId = Guid.Parse(reader.GetString(1)),
                UserId = Guid.Parse(reader.GetString(2)),
                SortOrder = reader.GetInt32(3),
                LocalFilePath = reader.IsDBNull(4) ? null : reader.GetString(4),
                ContentType = reader.IsDBNull(5) ? null : reader.GetString(5),
                CreatedOn = SqliteDateTime.Parse(reader.GetString(6))
            });
        }

        return photographs;
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
