using System.Text.Json;
using FishingLogBook.Mobile.Common.Offline.Sqlite;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Features.Catch.Offline.Stores;

public sealed class SqliteCatchWorkingCopyStore : ICatchWorkingCopyStore
{
    private static readonly JsonSerializerOptions MetadataOptions = new(JsonSerializerDefaults.Web);

    private readonly ISqliteDatabase _database;

    public SqliteCatchWorkingCopyStore(ISqliteDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    public async Task<bool> TryCreateAsync(
        LocalCatchRecordModel record,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        var created = false;
        await _database.ExecuteInTransactionAsync(
            async (connection, transaction, token) =>
            {
                if (await ExistsAsync(connection, transaction, record.Id, token))
                {
                    return;
                }

                await InsertCatchAsync(connection, transaction, record, token);
                foreach (var photograph in record.Photographs)
                {
                    await InsertPhotographAsync(connection, transaction, photograph, token);
                    await ExecuteAsync(
                        connection,
                        transaction,
                        """
                        INSERT INTO localphotographsyncs (photographid, catchid, userid, status, objectkey, updatedon)
                        VALUES ($photographId, $catchId, $userId, 'Recorded', NULL, $now);
                        """,
                        token,
                        ("$photographId", photograph.Id.ToString("D")),
                        ("$catchId", record.Id.ToString("D")),
                        ("$userId", record.UserId.ToString("D")),
                        ("$now", SqliteDateTime.Format(now)));
                }

                await ExecuteAsync(
                    connection,
                    transaction,
                    "INSERT INTO localcatchsyncs (catchid, userid, syncedrevision, syncedon) VALUES ($catchId, $userId, $revision, $now);",
                    token,
                    ("$catchId", record.Id.ToString("D")),
                    ("$userId", record.UserId.ToString("D")),
                    ("$revision", record.Revision),
                    ("$now", SqliteDateTime.Format(now)));
                created = true;
            },
            cancellationToken);
        return created;
    }

    private static async Task<bool> ExistsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid catchId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM localcatches WHERE id = $id;";
        command.Parameters.AddWithValue("$id", catchId.ToString("D"));
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static Task InsertCatchAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        LocalCatchRecordModel record,
        CancellationToken cancellationToken)
    {
        var location = record.Location;
        return ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO localcatches (
                id, userid, tripid, caughtbyuserid, recordedbyuserid, caughton,
                speciesname, weight, length, method, baitorlure, notes,
                placename, latitude, longitude, locationaccuracymetres, locationcapturedon,
                locationsource, locationvisibility, locationconsentversion, revision,
                lifecycle, createdon, updatedon)
            VALUES (
                $id, $userId, $tripId, $caughtBy, $recordedBy, $caughtOn,
                $species, $weight, $length, $method, $bait, $notes,
                $placeName, $latitude, $longitude, $accuracy, $capturedOn,
                $source, $visibility, $consent, $revision,
                'Saved', $createdOn, $updatedOn);
            """,
            cancellationToken,
            ("$id", record.Id.ToString("D")),
            ("$userId", record.UserId.ToString("D")),
            ("$tripId", record.TripId is null ? DBNull.Value : record.TripId.Value.ToString("D")),
            ("$caughtBy", record.CaughtByUserId.ToString("D")),
            ("$recordedBy", record.RecordedByUserId.ToString("D")),
            ("$caughtOn", SqliteDateTime.Format(record.CaughtOn)),
            ("$species", (object?)record.SpeciesName ?? DBNull.Value),
            ("$weight", (object?)record.Weight ?? DBNull.Value),
            ("$length", (object?)record.Length ?? DBNull.Value),
            ("$method", (object?)record.Method ?? DBNull.Value),
            ("$bait", (object?)record.BaitOrLure ?? DBNull.Value),
            ("$notes", (object?)record.Notes ?? DBNull.Value),
            ("$placeName", (object?)record.PlaceName ?? DBNull.Value),
            ("$latitude", (object?)location?.Latitude ?? DBNull.Value),
            ("$longitude", (object?)location?.Longitude ?? DBNull.Value),
            ("$accuracy", (object?)location?.AccuracyMetres ?? DBNull.Value),
            ("$capturedOn", location is null ? DBNull.Value : SqliteDateTime.Format(location.CapturedOn)),
            ("$source", (object?)location?.Source ?? DBNull.Value),
            ("$visibility", (object?)location?.Visibility ?? DBNull.Value),
            ("$consent", (object?)location?.ConsentVersion ?? DBNull.Value),
            ("$revision", record.Revision),
            ("$createdOn", SqliteDateTime.Format(record.CreatedOn)),
            ("$updatedOn", SqliteDateTime.Format(record.UpdatedOn)));
    }

    private static Task InsertPhotographAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        LocalCatchPhotographModel photograph,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO localcatchphotographs (
                id, catchid, userid, sortorder, localfilepath, contenttype, createdon, metadatajson)
            VALUES ($id, $catchId, $userId, $sortOrder, $path, $contentType, $createdOn, $metadata);
            """,
            cancellationToken,
            ("$id", photograph.Id.ToString("D")),
            ("$catchId", photograph.CatchId.ToString("D")),
            ("$userId", photograph.UserId.ToString("D")),
            ("$sortOrder", photograph.SortOrder),
            ("$path", (object?)photograph.LocalFilePath ?? DBNull.Value),
            ("$contentType", (object?)photograph.ContentType ?? DBNull.Value),
            ("$createdOn", SqliteDateTime.Format(photograph.CreatedOn)),
            ("$metadata", JsonSerializer.Serialize(photograph.Metadata, MetadataOptions)));
    }

    private static async Task<int> ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
