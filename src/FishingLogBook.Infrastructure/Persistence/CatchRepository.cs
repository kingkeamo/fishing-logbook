using Dapper;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Domain.Catches;
using FluentResults;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FishingLogBook.Infrastructure.Persistence;

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

    public async Task<Result<CatchPhotograph?>> GetPhotographAsync(
        GetCatchPhotographArgs args,
        CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                SELECT p."Id", p."CatchId", p."ContentType"
                FROM "CatchPhotograph" p
                INNER JOIN "Catch" c ON c."Id" = p."CatchId"
                WHERE p."Id" = @PhotographId
                  AND p."CatchId" = @CatchId
                  AND c."UserId" = @UserId;
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

    public async Task<Result<Catch>> UpsertAsync(Catch catchRecord, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                var existing = await LoadAsync(connection, transaction, catchRecord.Id, cancellationToken);
                if (existing is not null && existing.UserId != catchRecord.UserId)
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

    public async Task<Result> UpdateLocationVisibilityAsync(
        PersistCatchLocationVisibilityArgs args,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            const string sql = """
                UPDATE "Catch"
                SET "LocationVisibility" = @Visibility
                WHERE "Id" = @CatchId
                  AND "UserId" = @UserId
                  AND "Latitude" IS NOT NULL;
                """;
            var updated = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    args.CatchId,
                    args.UserId,
                    args.Visibility
                },
                cancellationToken: cancellationToken));
            return updated == 1
                ? Result.Ok()
                : Result.Fail("Failed to save the catch.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to update catch location visibility {CatchId}.", args.CatchId);
            return Result.Fail("Failed to save the catch.");
        }
    }

    private static async Task UpsertCatchRowAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Catch catchRecord,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO "Catch" (
                "Id",
                "UserId",
                "AnglerUserId",
                "RecordedByUserId",
                "CaughtOn",
                "SpeciesName",
                "Weight",
                "Length",
                "Method",
                "BaitOrLure",
                "Notes",
                "Latitude",
                "Longitude",
                "LocationAccuracyMetres",
                "LocationCapturedOn",
                "LocationSource",
                "LocationVisibility",
                "LocationConsentVersion")
            VALUES (
                @Id,
                @UserId,
                @AnglerUserId,
                @RecordedByUserId,
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
            ON CONFLICT ("Id") DO UPDATE SET
                "CaughtOn" = EXCLUDED."CaughtOn",
                "SpeciesName" = EXCLUDED."SpeciesName",
                "Weight" = EXCLUDED."Weight",
                "Length" = EXCLUDED."Length",
                "Method" = EXCLUDED."Method",
                "BaitOrLure" = EXCLUDED."BaitOrLure",
                "Notes" = EXCLUDED."Notes",
                "Latitude" = COALESCE(EXCLUDED."Latitude", "Catch"."Latitude"),
                "Longitude" = COALESCE(EXCLUDED."Longitude", "Catch"."Longitude"),
                "LocationAccuracyMetres" = CASE
                    WHEN EXCLUDED."Latitude" IS NOT NULL THEN EXCLUDED."LocationAccuracyMetres"
                    ELSE "Catch"."LocationAccuracyMetres"
                END,
                "LocationCapturedOn" = COALESCE(EXCLUDED."LocationCapturedOn", "Catch"."LocationCapturedOn"),
                "LocationSource" = COALESCE(EXCLUDED."LocationSource", "Catch"."LocationSource"),
                "LocationVisibility" = COALESCE(EXCLUDED."LocationVisibility", "Catch"."LocationVisibility"),
                "LocationConsentVersion" = COALESCE(EXCLUDED."LocationConsentVersion", "Catch"."LocationConsentVersion")
            WHERE "Catch"."UserId" = EXCLUDED."UserId";
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            ToRow(catchRecord),
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task ReplacePhotographsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Catch catchRecord,
        CancellationToken cancellationToken)
    {
        const string deleteSql = """DELETE FROM "CatchPhotograph" WHERE "CatchId" = @CatchId;""";
        await connection.ExecuteAsync(new CommandDefinition(
            deleteSql,
            new { CatchId = catchRecord.Id },
            transaction,
            cancellationToken: cancellationToken));

        const string insertSql = """
            INSERT INTO "CatchPhotograph" ("Id", "CatchId", "ContentType")
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
                "Id",
                "UserId",
                COALESCE("AnglerUserId", "UserId") AS "AnglerUserId",
                COALESCE("RecordedByUserId", "UserId") AS "RecordedByUserId",
                "CaughtOn",
                "SpeciesName",
                "Weight",
                "Length",
                "Method",
                "BaitOrLure",
                "Notes",
                "Latitude",
                "Longitude",
                "LocationAccuracyMetres",
                "LocationCapturedOn",
                "LocationSource",
                "LocationVisibility",
                "LocationConsentVersion"
            FROM "Catch"
            WHERE "Id" = @Id;
            """;
        var catchRow = await connection.QuerySingleOrDefaultAsync<CatchRow>(new CommandDefinition(
            catchSql,
            new { Id = id },
            transaction,
            cancellationToken: cancellationToken));
        if (catchRow is null)
        {
            return null;
        }

        const string photographSql = """
            SELECT "Id", "CatchId", "ContentType"
            FROM "CatchPhotograph"
            WHERE "CatchId" = @CatchId
            ORDER BY "Id";
            """;
        var photographs = await connection.QueryAsync<CatchPhotograph>(new CommandDefinition(
            photographSql,
            new { CatchId = id },
            transaction,
            cancellationToken: cancellationToken));
        catchRow.Photographs = photographs.ToArray();

        return _mapper.Map<Catch>(catchRow);
    }

    private static object ToRow(Catch catchRecord)
    {
        return new
        {
            catchRecord.Id,
            catchRecord.UserId,
            catchRecord.AnglerUserId,
            catchRecord.RecordedByUserId,
            CaughtOn = catchRecord.CaughtOn.ToUniversalTime(),
            catchRecord.SpeciesName,
            catchRecord.Weight,
            catchRecord.Length,
            catchRecord.Method,
            catchRecord.BaitOrLure,
            catchRecord.Notes,
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

    internal sealed class CatchRow
    {
        public Guid Id { get; init; }

        public Guid UserId { get; init; }

        public Guid AnglerUserId { get; init; }

        public Guid RecordedByUserId { get; init; }

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
}
