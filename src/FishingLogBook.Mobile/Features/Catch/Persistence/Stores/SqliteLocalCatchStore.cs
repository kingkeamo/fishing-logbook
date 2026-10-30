using FishingLogBook.Mobile.Features.Catch.Persistence.Enums;
using FishingLogBook.Mobile.Features.Catch.Persistence.Models;
using FishingLogBook.Mobile.Features.LocalPersistence.Sqlite;
using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Features.Catch.Persistence.Stores;

public sealed class SqliteLocalCatchStore : ILocalCatchStore
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly ISqliteDatabase _database;

    public SqliteLocalCatchStore(ISqliteConnectionFactory connectionFactory, ISqliteDatabase database)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(database);
        _connectionFactory = connectionFactory;
        _database = database;
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
            SELECT user_id
            FROM local_trips
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
            INSERT INTO local_trips (id, user_id, title, updated_at)
            VALUES ($id, $userId, $title, $updatedAt)
            ON CONFLICT(id) DO UPDATE SET
                title = COALESCE(excluded.title, local_trips.title),
                updated_at = excluded.updated_at
            WHERE local_trips.user_id = excluded.user_id;
            """;
        command.Parameters.AddWithValue("$id", trip.Id.ToString("D"));
        command.Parameters.AddWithValue("$userId", trip.UserId.ToString("D"));
        command.Parameters.AddWithValue("$title", (object?)trip.Title ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedAt", SqliteDateTime.Format(trip.UpdatedAt));
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
            SELECT revision, created_at
            FROM local_catches
            WHERE id = $id
              AND user_id = $userId
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
            CreatedAt = SqliteDateTime.Parse(reader.GetString(1))
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
            SELECT user_id
            FROM local_catches
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
            INSERT INTO local_catches (
                id, user_id, trip_id, caught_by_user_id, recorded_by_user_id, caught_on,
                species_name, weight_grams, length_millimetres, method, bait_or_lure, notes,
                place_name, latitude, longitude, location_accuracy_metres, location_captured_on,
                location_source, location_visibility, location_consent_version, revision,
                lifecycle, created_at, updated_at)
            VALUES (
                $id, $userId, $tripId, $caughtByUserId, $recordedByUserId, $caughtOn,
                $speciesName, $weightGrams, $lengthMillimetres, $method, $baitOrLure, $notes,
                $placeName, $latitude, $longitude, $locationAccuracyMetres, $locationCapturedOn,
                $locationSource, $locationVisibility, $locationConsentVersion, $revision,
                $lifecycle, $createdAt, $updatedAt)
            ON CONFLICT(id) DO UPDATE SET
                trip_id = excluded.trip_id,
                caught_by_user_id = excluded.caught_by_user_id,
                recorded_by_user_id = excluded.recorded_by_user_id,
                caught_on = excluded.caught_on,
                species_name = excluded.species_name,
                weight_grams = excluded.weight_grams,
                length_millimetres = excluded.length_millimetres,
                method = excluded.method,
                bait_or_lure = excluded.bait_or_lure,
                notes = excluded.notes,
                place_name = excluded.place_name,
                latitude = excluded.latitude,
                longitude = excluded.longitude,
                location_accuracy_metres = excluded.location_accuracy_metres,
                location_captured_on = excluded.location_captured_on,
                location_source = excluded.location_source,
                location_visibility = excluded.location_visibility,
                location_consent_version = excluded.location_consent_version,
                revision = excluded.revision,
                lifecycle = excluded.lifecycle,
                updated_at = excluded.updated_at
            WHERE local_catches.user_id = excluded.user_id;
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
            DELETE FROM local_catch_photographs
            WHERE catch_id = $catchId
              AND user_id = $userId;
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
                INSERT INTO local_catch_photographs (
                    id, catch_id, user_id, sort_order, local_file_path, content_type, created_at)
                VALUES (
                    $id, $catchId, $userId, $sortOrder, $localFilePath, $contentType, $createdAt);
                """;
            insertCommand.Parameters.AddWithValue("$id", photograph.Id.ToString("D"));
            insertCommand.Parameters.AddWithValue("$catchId", photograph.CatchId.ToString("D"));
            insertCommand.Parameters.AddWithValue("$userId", photograph.UserId.ToString("D"));
            insertCommand.Parameters.AddWithValue("$sortOrder", photograph.SortOrder);
            insertCommand.Parameters.AddWithValue("$localFilePath", (object?)photograph.LocalFilePath ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("$contentType", (object?)photograph.ContentType ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("$createdAt", SqliteDateTime.Format(photograph.CreatedAt));
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<LocalCatchRecordModel?> GetAsync(
        Guid userId,
        Guid catchId,
        CancellationToken cancellationToken)
    {
        await _database.EnsureInitializedAsync(cancellationToken);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = CatchSelectSql + " WHERE c.id = $id AND c.user_id = $userId LIMIT 1;";
        command.Parameters.AddWithValue("$id", catchId.ToString("D"));
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        var catches = await ReadCatchesAsync(connection, command, cancellationToken);
        return catches.Count == 0 ? null : catches[0];
    }

    public async Task<IReadOnlyList<LocalCatchRecordModel>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _database.EnsureInitializedAsync(cancellationToken);
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = CatchSelectSql + " WHERE c.user_id = $userId ORDER BY c.caught_on DESC, c.id ASC;";
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
        command.Parameters.AddWithValue("$weightGrams", (object?)catchRecord.WeightGrams ?? DBNull.Value);
        command.Parameters.AddWithValue("$lengthMillimetres", (object?)catchRecord.LengthMillimetres ?? DBNull.Value);
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
        command.Parameters.AddWithValue("$createdAt", SqliteDateTime.Format(catchRecord.CreatedAt));
        command.Parameters.AddWithValue("$updatedAt", SqliteDateTime.Format(catchRecord.UpdatedAt));
    }

    private const string CatchSelectSql =
        """
        SELECT
            c.id, c.user_id, c.trip_id, c.caught_by_user_id, c.recorded_by_user_id, c.caught_on,
            c.species_name, c.weight_grams, c.length_millimetres, c.method, c.bait_or_lure, c.notes,
            c.place_name, c.latitude, c.longitude, c.location_accuracy_metres, c.location_captured_on,
            c.location_source, c.location_visibility, c.location_consent_version, c.revision,
            c.lifecycle, c.created_at, c.updated_at
        FROM local_catches c
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
                WeightGrams = catchRecord.WeightGrams,
                LengthMillimetres = catchRecord.LengthMillimetres,
                Method = catchRecord.Method,
                BaitOrLure = catchRecord.BaitOrLure,
                Notes = catchRecord.Notes,
                PlaceName = catchRecord.PlaceName,
                Location = catchRecord.Location,
                Revision = catchRecord.Revision,
                Lifecycle = catchRecord.Lifecycle,
                CreatedAt = catchRecord.CreatedAt,
                UpdatedAt = catchRecord.UpdatedAt,
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
            WeightGrams = reader.IsDBNull(7) ? null : reader.GetDouble(7),
            LengthMillimetres = reader.IsDBNull(8) ? null : reader.GetDouble(8),
            Method = reader.IsDBNull(9) ? null : reader.GetString(9),
            BaitOrLure = reader.IsDBNull(10) ? null : reader.GetString(10),
            Notes = reader.IsDBNull(11) ? null : reader.GetString(11),
            PlaceName = reader.IsDBNull(12) ? null : reader.GetString(12),
            Location = location,
            Revision = reader.GetInt64(20),
            Lifecycle = Enum.Parse<LocalCatchLifecycleEnum>(reader.GetString(21)),
            CreatedAt = SqliteDateTime.Parse(reader.GetString(22)),
            UpdatedAt = SqliteDateTime.Parse(reader.GetString(23)),
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
            SELECT id, catch_id, user_id, sort_order, local_file_path, content_type, created_at
            FROM local_catch_photographs
            WHERE catch_id = $catchId
              AND user_id = $userId
            ORDER BY sort_order ASC, id ASC;
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
                CreatedAt = SqliteDateTime.Parse(reader.GetString(6))
            });
        }

        return photographs;
    }
}
