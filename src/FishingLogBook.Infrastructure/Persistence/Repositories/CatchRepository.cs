using Dapper;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Contracts.Repositories;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Domain.Catches;
using FluentResults;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FishingLogBook.Infrastructure.Persistence.Repositories;

public sealed class CatchRepository : ICatchRepository
{
    private const string FailedMessage = "Failed to save the catch.";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<CatchRepository> _logger;
    private readonly IMapper _mapper;

    public CatchRepository(IDbConnectionFactory connectionFactory, ILogger<CatchRepository> logger, IMapper mapper)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _mapper = mapper;
    }

    public async Task<Result<Catch?>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            return Result.Ok(await LoadAsync(connection, transaction: null, id, cancellationToken));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load the catch {CatchId}.", id);
            return Result.Fail<Catch?>(FailedMessage);
        }
    }

    public async Task<Result<CatchDetail?>> GetDetailForUserAsync(
        Guid catchId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            return Result.Ok(await LoadDetailForUserAsync(connection, catchId, userId, cancellationToken));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load the catch {CatchId}.", catchId);
            return Result.Fail<CatchDetail?>(FailedMessage);
        }
    }

    public async Task<Result<IReadOnlyList<CatchDetail>>> GetActivityForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var catches = await LoadActivityForUserAsync(connection, userId, cancellationToken);
            return Result.Ok(catches);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load catches for user {UserId}.", userId);
            return Result.Fail<IReadOnlyList<CatchDetail>>(FailedMessage);
        }
    }

    public async Task<Result<CatchPhotograph?>> GetPhotographAsync(
        GetCatchPhotographArgs args,
        CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                SELECT p.id, p.catchid, p.contenttype
                FROM catchphotographs p
                INNER JOIN catches c ON c.id = p.catchid
                WHERE p.id = @PhotographId
                  AND p.catchid = @CatchId
                  AND c.caughtbyuserid = @CaughtByUserId;
                """;
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var photograph = await connection.QuerySingleOrDefaultAsync<CatchPhotograph>(
                new CommandDefinition(
                    sql,
                    args,
                    cancellationToken: cancellationToken));
            return Result.Ok(photograph);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to load catch photograph {PhotographId} for catch {CatchId}.",
                args.PhotographId,
                args.CatchId);
            return Result.Fail<CatchPhotograph?>(FailedMessage);
        }
    }

    public async Task<Result> DeletePhotographAsync(
        GetCatchPhotographArgs args,
        CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                DELETE FROM catchphotographs p
                USING catches c
                WHERE p.id = @PhotographId
                  AND p.catchid = @CatchId
                  AND c.id = p.catchid
                  AND c.caughtbyuserid = @CaughtByUserId;
                """;
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                args,
                cancellationToken: cancellationToken));
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to delete catch photograph {PhotographId} for catch {CatchId}.",
                args.PhotographId,
                args.CatchId);
            return Result.Fail(FailedMessage);
        }
    }

    public async Task<Result<Catch>> UpsertAsync(Catch catchRecord, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                var existing = await LoadAsync(connection, transaction, catchRecord.Id, cancellationToken);
                if (existing is not null && existing.CaughtByUserId != catchRecord.CaughtByUserId)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Fail<Catch>(new CatchOwnershipConflictError());
                }

                await UpsertCatchRowAsync(connection, transaction, catchRecord, cancellationToken);
                if (existing is null || !HaveSamePhotographs(existing, catchRecord))
                {
                    await ReplacePhotographsAsync(connection, transaction, catchRecord, cancellationToken);
                }
                await transaction.CommitAsync(cancellationToken);
                var saved = await LoadAsync(connection, transaction: null, catchRecord.Id, cancellationToken);
                return saved is null
                    ? Result.Fail<Catch>(FailedMessage)
                    : Result.Ok(saved);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to save the catch {CatchId}.", catchRecord.Id);
            return Result.Fail<Catch>(FailedMessage);
        }
    }

    public async Task<Result<bool>> AssociateTripAsync(
        PersistCatchTripArgs args,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            const string sql = """
                UPDATE catches
                SET tripid = @TripId
                WHERE id = @CatchId
                  AND caughtbyuserid = @CaughtByUserId
                  AND tripid IS NULL;
                """;
            var updated = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    args.CatchId,
                    args.CaughtByUserId,
                    args.TripId
                },
                cancellationToken: cancellationToken));
            return Result.Ok(updated == 1);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to associate catch {CatchId} with a trip.", args.CatchId);
            return Result.Fail<bool>(FailedMessage);
        }
    }

    public async Task<Result> UpdateLocationVisibilityAsync(
        PersistCatchLocationVisibilityArgs args,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            const string sql = """
                UPDATE catches
                SET locationvisibility = @Visibility
                WHERE id = @CatchId
                  AND caughtbyuserid = @CaughtByUserId
                  AND latitude IS NOT NULL;
                """;
            var updated = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    args.CatchId,
                    args.CaughtByUserId,
                    args.Visibility
                },
                cancellationToken: cancellationToken));
            return updated == 1
                ? Result.Ok()
                : Result.Fail("Failed to save the catch.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to update catches location visibility {CatchId}.", args.CatchId);
            return Result.Fail("Failed to save the catch.");
        }
    }

    public async Task<Result> CorrectAnglerAsync(
        PersistCatchAnglerArgs args,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            const string sql = """
                UPDATE catches
                SET caughtbyuserid = @CaughtByUserId
                WHERE id = @CatchId
                  AND tripid IS NOT NULL;
                """;
            var updated = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    args.CatchId,
                    args.CaughtByUserId
                },
                cancellationToken: cancellationToken));
            return updated == 1
                ? Result.Ok()
                : Result.Fail("Failed to correct the catch angler.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to correct the angler for catch {CatchId}.", args.CatchId);
            return Result.Fail("Failed to correct the catch angler.");
        }
    }

    private static async Task UpsertCatchRowAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Catch catchRecord,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO catches (
                id,
                caughtbyuserid,
                recordedbyuserid,
                tripid,
                caughton,
                speciesname,
                weight,
                length,
                method,
                baitorlure,
                notes,
                latitude,
                longitude,
                locationaccuracymetres,
                locationcapturedon,
                locationsource,
                locationvisibility,
                locationconsentversion)
            VALUES (
                @Id,
                @CaughtByUserId,
                @RecordedByUserId,
                @TripId,
                @CaughtOn,
                @SpeciesName,
                @Weight,
                @Length,
                @Method,
                @BaitOrLure,
                @Notes,
                @Latitude,
                @Longitude,
                @LocationAccuracyMetres,
                @LocationCapturedOn,
                @LocationSource,
                @LocationVisibility,
                @LocationConsentVersion)
            ON CONFLICT (id) DO UPDATE SET
                caughtbyuserid = EXCLUDED.caughtbyuserid,
                tripid = EXCLUDED.tripid,
                caughton = EXCLUDED.caughton,
                speciesname = EXCLUDED.speciesname,
                weight = EXCLUDED.weight,
                length = EXCLUDED.length,
                method = EXCLUDED.method,
                baitorlure = EXCLUDED.baitorlure,
                notes = EXCLUDED.notes,
                latitude = COALESCE(EXCLUDED.latitude, catches.latitude),
                longitude = COALESCE(EXCLUDED.longitude, catches.longitude),
                locationaccuracymetres = CASE
                    WHEN EXCLUDED.latitude IS NOT NULL THEN EXCLUDED.locationaccuracymetres
                    ELSE catches.locationaccuracymetres
                END,
                locationcapturedon = COALESCE(EXCLUDED.locationcapturedon, catches.locationcapturedon),
                locationsource = COALESCE(EXCLUDED.locationsource, catches.locationsource),
                locationvisibility = COALESCE(EXCLUDED.locationvisibility, catches.locationvisibility),
                locationconsentversion = COALESCE(EXCLUDED.locationconsentversion, catches.locationconsentversion)
            WHERE catches.caughtbyuserid = EXCLUDED.caughtbyuserid;
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            ToParameters(catchRecord),
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task ReplacePhotographsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Catch catchRecord,
        CancellationToken cancellationToken)
    {
        const string deleteSql = """DELETE FROM catchphotographs WHERE catchid = @CatchId;""";
        await connection.ExecuteAsync(new CommandDefinition(
            deleteSql,
            new { CatchId = catchRecord.Id },
            transaction,
            cancellationToken: cancellationToken));

        const string insertSql = """
            INSERT INTO catchphotographs (id, catchid, contenttype)
            VALUES (@Id, @CatchId, @ContentType);
            """;
        foreach (var photograph in catchRecord.Photographs)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                insertSql,
                new { photograph.Id, photograph.CatchId, photograph.ContentType },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    private async Task<Catch?> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid id,
        CancellationToken cancellationToken)
    {
        const string catchSql = """
            SELECT
                id,
                caughtbyuserid,
                recordedbyuserid,
                tripid,
                caughton,
                speciesname,
                weight,
                length,
                method,
                baitorlure,
                notes,
                latitude,
                longitude,
                locationaccuracymetres,
                locationcapturedon,
                locationsource,
                locationvisibility,
                locationconsentversion
            FROM catches
            WHERE id = @Id;
            """;
        var catchRow = await connection.QuerySingleOrDefaultAsync<CatchPersistenceRow>(new CommandDefinition(
            catchSql,
            new { Id = id },
            transaction,
            cancellationToken: cancellationToken));
        if (catchRow is null)
        {
            return null;
        }

        catchRow.Photographs = await LoadPhotographsAsync(connection, transaction, id, cancellationToken);
        return _mapper.Map<Catch>(catchRow);
    }

    private static async Task<IReadOnlyList<CatchPhotograph>> LoadPhotographsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid catchId,
        CancellationToken cancellationToken)
    {
        const string photographSql = """
            SELECT id, catchid, contenttype
            FROM catchphotographs
            WHERE catchid = @CatchId
            ORDER BY id;
            """;
        var photographs = await connection.QueryAsync<CatchPhotograph>(new CommandDefinition(
            photographSql,
            new { CatchId = catchId },
            transaction,
            cancellationToken: cancellationToken));
        return photographs.ToArray();
    }

    private async Task<CatchDetail?> LoadDetailForUserAsync(
        NpgsqlConnection connection,
        Guid catchId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string catchSql = """
            SELECT
                c.id,
                c.caughtbyuserid,
                c.recordedbyuserid,
                c.tripid,
                c.caughton,
                c.speciesname,
                c.weight,
                c.length,
                c.method,
                c.baitorlure,
                c.notes,
                c.latitude,
                c.longitude,
                c.locationaccuracymetres,
                c.locationcapturedon,
                c.locationsource,
                c.locationvisibility,
                c.locationconsentversion,
                angler_profile.displayname AS anglername,
                recorder_profile.displayname AS recordedbyname
            FROM catches c
            LEFT JOIN profiles angler_profile
                ON angler_profile.userid = c.caughtbyuserid
            LEFT JOIN profiles recorder_profile
                ON recorder_profile.userid = c.recordedbyuserid
            WHERE c.id = @CatchId
              AND (
                c.caughtbyuserid = @UserId
                OR c.recordedbyuserid = @UserId
                OR EXISTS (
                    SELECT 1
                    FROM trips t
                    WHERE t.id = c.tripid
                      AND t.owneruserid = @UserId
                )
                OR EXISTS (
                    SELECT 1
                    FROM tripparticipants tp
                    WHERE tp.tripid = c.tripid
                      AND tp.userid = @UserId
                      AND tp.status = 'Accepted'
                      AND tp.removedon IS NULL
                )
              );
            """;
        var row = await connection.QuerySingleOrDefaultAsync<CatchDetailRow>(new CommandDefinition(
            catchSql,
            new { CatchId = catchId, UserId = userId },
            cancellationToken: cancellationToken));
        if (row is null)
        {
            return null;
        }

        row.Photographs = await LoadPhotographsAsync(connection, transaction: null, catchId, cancellationToken);
        return ToDetail(row);
    }

    private CatchDetail ToDetail(CatchDetailRow row)
    {
        return new CatchDetail
        {
            Catch = _mapper.Map<Catch>(row),
            AnglerName = row.AnglerName,
            RecordedByName = row.RecordedByName
        };
    }

    private async Task<IReadOnlyList<CatchDetail>> LoadActivityForUserAsync(
        NpgsqlConnection connection,
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                c.id,
                c.caughtbyuserid,
                c.recordedbyuserid,
                c.tripid,
                c.caughton,
                c.speciesname,
                c.weight,
                c.length,
                c.method,
                c.baitorlure,
                c.notes,
                c.latitude,
                c.longitude,
                c.locationaccuracymetres,
                c.locationcapturedon,
                c.locationsource,
                c.locationvisibility,
                c.locationconsentversion,
                angler_profile.displayname AS anglername,
                recorder_profile.displayname AS recordedbyname,
                p.id AS photographid,
                p.catchid AS photographcatchid,
                p.contenttype AS photographcontenttype
            FROM catches c
            LEFT JOIN catchphotographs p ON p.catchid = c.id
            LEFT JOIN profiles angler_profile
                ON angler_profile.userid = c.caughtbyuserid
            LEFT JOIN profiles recorder_profile
                ON recorder_profile.userid = c.recordedbyuserid
            WHERE c.caughtbyuserid = @UserId
               OR c.recordedbyuserid = @UserId
            ORDER BY c.caughton DESC, p.id;
            """;

        var rowsById = new Dictionary<Guid, CatchDetailRow>();
        var photographsById = new Dictionary<Guid, List<CatchPhotograph>>();
        var order = new List<Guid>();
        await connection.QueryAsync<CatchDetailRow, CatchPhotographRow, CatchDetailRow>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken),
            (row, photographRow) =>
            {
                if (!rowsById.ContainsKey(row.Id))
                {
                    rowsById[row.Id] = row;
                    photographsById[row.Id] = [];
                    order.Add(row.Id);
                }

                if (photographRow is not null)
                {
                    photographsById[row.Id].Add(new CatchPhotograph
                    {
                        Id = photographRow.PhotographId,
                        CatchId = photographRow.PhotographCatchId,
                        ContentType = photographRow.PhotographContentType
                    });
                }

                return row;
            },
            splitOn: "photographid");

        foreach (var id in order)
        {
            rowsById[id].Photographs = photographsById[id];
        }

        return order.Select(id => ToDetail(rowsById[id])).ToArray();
    }

    private static CatchPersistenceParameters ToParameters(Catch catchRecord)
    {
        return new CatchPersistenceParameters
        {
            Id = catchRecord.Id,
            CaughtByUserId = catchRecord.CaughtByUserId,
            RecordedByUserId = catchRecord.RecordedByUserId,
            TripId = catchRecord.TripId,
            CaughtOn = catchRecord.CaughtOn.ToUniversalTime(),
            SpeciesName = catchRecord.SpeciesName,
            Weight = catchRecord.Weight,
            Length = catchRecord.Length,
            Method = catchRecord.Method,
            BaitOrLure = catchRecord.BaitOrLure,
            Notes = catchRecord.Notes,
            Latitude = catchRecord.Location?.Latitude,
            Longitude = catchRecord.Location?.Longitude,
            LocationAccuracyMetres = catchRecord.Location?.AccuracyMetres,
            LocationCapturedOn = catchRecord.Location?.CapturedOn.ToUniversalTime(),
            LocationSource = catchRecord.Location?.Source,
            LocationVisibility = catchRecord.Location?.Visibility,
            LocationConsentVersion = catchRecord.Location?.ConsentVersion
        };
    }

    private static bool HaveSamePhotographs(Catch existing, Catch incoming)
    {
        if (existing.Photographs.Count != incoming.Photographs.Count)
        {
            return false;
        }

        var incomingById = incoming.Photographs.ToDictionary(photograph => photograph.Id);
        foreach (var photograph in existing.Photographs)
        {
            if (!incomingById.TryGetValue(photograph.Id, out var match))
            {
                return false;
            }

            if (match.CatchId != photograph.CatchId
                || !string.Equals(
                    match.ContentType,
                    photograph.ContentType,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    internal sealed class CatchPersistenceRow
    {
        public Guid Id { get; init; }

        public Guid CaughtByUserId { get; init; }

        public Guid RecordedByUserId { get; init; }

        public Guid? TripId { get; init; }

        public DateTimeOffset CaughtOn { get; init; }

        public string? SpeciesName { get; init; }

        public decimal? Weight { get; init; }

        public decimal? Length { get; init; }

        public string? Method { get; init; }

        public string? BaitOrLure { get; init; }

        public string? Notes { get; init; }

        public double? Latitude { get; init; }

        public double? Longitude { get; init; }

        public double? LocationAccuracyMetres { get; init; }

        public DateTimeOffset? LocationCapturedOn { get; init; }

        public string? LocationSource { get; init; }

        public string? LocationVisibility { get; init; }

        public string? LocationConsentVersion { get; init; }

        public IReadOnlyList<CatchPhotograph> Photographs { get; set; } = [];
    }

    internal sealed class CatchDetailRow
    {
        public Guid Id { get; init; }

        public Guid CaughtByUserId { get; init; }

        public Guid RecordedByUserId { get; init; }

        public Guid? TripId { get; init; }

        public DateTimeOffset CaughtOn { get; init; }

        public string? SpeciesName { get; init; }

        public decimal? Weight { get; init; }

        public decimal? Length { get; init; }

        public string? Method { get; init; }

        public string? BaitOrLure { get; init; }

        public string? Notes { get; init; }

        public double? Latitude { get; init; }

        public double? Longitude { get; init; }

        public double? LocationAccuracyMetres { get; init; }

        public DateTimeOffset? LocationCapturedOn { get; init; }

        public string? LocationSource { get; init; }

        public string? LocationVisibility { get; init; }

        public string? LocationConsentVersion { get; init; }

        public string? AnglerName { get; init; }

        public string? RecordedByName { get; init; }

        public IReadOnlyList<CatchPhotograph> Photographs { get; set; } = [];
    }

    private sealed class CatchPhotographRow
    {
        public Guid PhotographId { get; init; }

        public Guid PhotographCatchId { get; init; }

        public string PhotographContentType { get; init; } = string.Empty;
    }

    private sealed class CatchPersistenceParameters
    {
        public Guid Id { get; init; }

        public Guid CaughtByUserId { get; init; }

        public Guid RecordedByUserId { get; init; }

        public Guid? TripId { get; init; }

        public DateTimeOffset CaughtOn { get; init; }

        public string? SpeciesName { get; init; }

        public decimal? Weight { get; init; }

        public decimal? Length { get; init; }

        public string? Method { get; init; }

        public string? BaitOrLure { get; init; }

        public string? Notes { get; init; }

        public double? Latitude { get; init; }

        public double? Longitude { get; init; }

        public double? LocationAccuracyMetres { get; init; }

        public DateTimeOffset? LocationCapturedOn { get; init; }

        public string? LocationSource { get; init; }

        public string? LocationVisibility { get; init; }

        public string? LocationConsentVersion { get; init; }
    }
}
