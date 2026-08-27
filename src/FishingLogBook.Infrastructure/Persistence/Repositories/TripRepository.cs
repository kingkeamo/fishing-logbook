using Dapper;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FluentResults;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FishingLogBook.Infrastructure.Persistence.Repositories;

public sealed class TripRepository : ITripRepository
{
    private const string FailedMessage = "Failed to save the trip.";

    private const string SelectSql = """
        SELECT
            "Id",
            "OwnerUserId",
            "Title",
            "PlaceName",
            "Status",
            "StartedOn",
            "EndedOn",
            "Latitude",
            "Longitude",
            "LocationAccuracyMetres",
            "LocationCapturedOn",
            "LocationSource",
            "LocationVisibility",
            "LocationConsentVersion",
            "CreatedOn",
            "UpdatedOn"
        FROM "Trip"
        """;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<TripRepository> _logger;
    private readonly IMapper _mapper;

    public TripRepository(
        IDbConnectionFactory connectionFactory,
        ILogger<TripRepository> logger,
        IMapper mapper)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _mapper = mapper;
    }

    public async Task<Result<Trip?>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            return Result.Ok(await LoadAsync(connection, transaction: null, id, cancellationToken));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load the trip {TripId}.", id);
            return Result.Fail<Trip?>(FailedMessage);
        }
    }

    public async Task<Result<IReadOnlyList<TripSummary>>> GetSummariesByOwnerUserIdAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            const string sql = """
                SELECT
                    t."Id",
                    t."Status",
                    t."StartedOn",
                    t."EndedOn",
                    t."Title",
                    t."PlaceName",
                    (SELECT COUNT(*) FROM "Catch" c WHERE c."TripId" = t."Id") AS "CatchCount",
                    (SELECT COUNT(*) FROM "TripPhotograph" p WHERE p."TripId" = t."Id") AS "PhotographCount",
                    (SELECT COUNT(*) FROM "TripNote" n WHERE n."TripId" = t."Id") AS "NoteCount"
                FROM "Trip" t
                WHERE t."OwnerUserId" = @OwnerUserId
                ORDER BY t."StartedOn" DESC, t."Id" DESC;
                """;
            var rows = await connection.QueryAsync<TripSummaryRow>(new CommandDefinition(
                sql,
                new { OwnerUserId = ownerUserId },
                cancellationToken: cancellationToken));
            IReadOnlyList<TripSummary> summaries =
            [
                .. rows.Select(row => new TripSummary
                {
                    Id = row.Id,
                    Status = ToStatus(row.Status),
                    StartedOn = row.StartedOn,
                    EndedOn = row.EndedOn,
                    Title = row.Title,
                    PlaceName = row.PlaceName,
                    CatchCount = row.CatchCount,
                    PhotographCount = row.PhotographCount,
                    NoteCount = row.NoteCount
                })
            ];
            return Result.Ok(summaries);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load trip summaries for user {UserId}.", ownerUserId);
            return Result.Fail<IReadOnlyList<TripSummary>>(FailedMessage);
        }
    }

    public async Task<Result<IReadOnlyList<TripCatchSummary>>> GetCatchSummariesByTripIdAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            const string sql = """
                SELECT
                    c."Id",
                    c."UserId",
                    c."CaughtOn",
                    c."SpeciesName",
                    c."Weight",
                    c."Length",
                    (
                        SELECT p."Id"
                        FROM "CatchPhotograph" p
                        WHERE p."CatchId" = c."Id"
                        ORDER BY p."Id"
                        LIMIT 1
                    ) AS "PhotographId"
                FROM "Catch" c
                WHERE c."TripId" = @TripId
                ORDER BY c."CaughtOn", c."Id";
                """;
            var rows = await connection.QueryAsync<TripCatchSummary>(new CommandDefinition(
                sql,
                new { TripId = tripId },
                cancellationToken: cancellationToken));
            return Result.Ok<IReadOnlyList<TripCatchSummary>>([.. rows]);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load catch summaries for trip {TripId}.", tripId);
            return Result.Fail<IReadOnlyList<TripCatchSummary>>(FailedMessage);
        }
    }

    public async Task<Result<Trip>> UpsertAsync(Trip trip, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                var existing = await LoadAsync(connection, transaction, trip.Id, cancellationToken);
                if (existing is not null && existing.OwnerUserId != trip.OwnerUserId)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Fail<Trip>(new TripOwnershipConflictError());
                }

                var demoted = await DemoteConflictingActiveAsync(
                    connection,
                    transaction,
                    trip,
                    cancellationToken);
                await UpsertRowAsync(connection, transaction, demoted, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                var saved = await LoadAsync(connection, transaction: null, trip.Id, cancellationToken);
                return saved is null
                    ? Result.Fail<Trip>(FailedMessage)
                    : Result.Ok(saved);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            _logger.LogWarning(
                exception,
                "An active trip already exists for the owner of trip {TripId}.",
                trip.Id);
            return Result.Fail<Trip>(new TripAlreadyActiveError());
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to save the trip {TripId}.", trip.Id);
            return Result.Fail<Trip>(FailedMessage);
        }
    }

    private static async Task UpsertRowAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Trip trip,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO "Trip" (
                "Id",
                "OwnerUserId",
                "Title",
                "PlaceName",
                "Status",
                "StartedOn",
                "EndedOn",
                "Latitude",
                "Longitude",
                "LocationAccuracyMetres",
                "LocationCapturedOn",
                "LocationSource",
                "LocationVisibility",
                "LocationConsentVersion",
                "UpdatedOn"
            )
            VALUES (
                @Id,
                @OwnerUserId,
                @Title,
                @PlaceName,
                @Status,
                @StartedOn,
                @EndedOn,
                @Latitude,
                @Longitude,
                @LocationAccuracyMetres,
                @LocationCapturedOn,
                @LocationSource,
                @LocationVisibility,
                @LocationConsentVersion,
                now()
            )
            ON CONFLICT ("Id") DO UPDATE SET
                "Title" = EXCLUDED."Title",
                "PlaceName" = EXCLUDED."PlaceName",
                "Status" = EXCLUDED."Status",
                "StartedOn" = EXCLUDED."StartedOn",
                "EndedOn" = EXCLUDED."EndedOn",
                "Latitude" = EXCLUDED."Latitude",
                "Longitude" = EXCLUDED."Longitude",
                "LocationAccuracyMetres" = EXCLUDED."LocationAccuracyMetres",
                "LocationCapturedOn" = EXCLUDED."LocationCapturedOn",
                "LocationSource" = EXCLUDED."LocationSource",
                "LocationVisibility" = EXCLUDED."LocationVisibility",
                "LocationConsentVersion" = EXCLUDED."LocationConsentVersion",
                "UpdatedOn" = now();
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            ToParameters(trip),
            transaction,
            cancellationToken: cancellationToken));
    }

    private async Task<Trip> DemoteConflictingActiveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Trip trip,
        CancellationToken cancellationToken)
    {
        if (!trip.IsActive)
        {
            return trip;
        }

        const string sql = $"""
            {SelectSql}
            WHERE "OwnerUserId" = @OwnerUserId
              AND "Status" = 'Active'
              AND "Id" <> @Id
            LIMIT 1;
            """;
        var row = await connection.QuerySingleOrDefaultAsync<TripPersistenceRow>(new CommandDefinition(
            sql,
            new { trip.OwnerUserId, trip.Id },
            transaction,
            cancellationToken: cancellationToken));
        if (row is null)
        {
            return trip;
        }

        var existing = _mapper.Map<Trip>(row);
        if (trip.OutranksActive(existing))
        {
            await UpsertRowAsync(
                connection,
                transaction,
                existing.CompletedAt(trip.StartedOn),
                cancellationToken);
            _logger.LogWarning(
                "Completed the earlier active trip {TripId} so that {IncomingTripId} could become active.",
                existing.Id,
                trip.Id);
            return trip;
        }

        _logger.LogWarning(
            "Stored trip {TripId} as completed because active trip {ActiveTripId} started later.",
            trip.Id,
            existing.Id);
        return trip.CompletedAt(existing.StartedOn);
    }

    private static TripPersistenceParameters ToParameters(Trip trip)
    {
        return new TripPersistenceParameters
        {
            Id = trip.Id,
            OwnerUserId = trip.OwnerUserId,
            Title = trip.Title,
            PlaceName = trip.PlaceName,
            Status = trip.Status.ToString(),
            StartedOn = trip.StartedOn.ToUniversalTime(),
            EndedOn = trip.EndedOn?.ToUniversalTime(),
            Latitude = trip.Location?.Latitude,
            Longitude = trip.Location?.Longitude,
            LocationAccuracyMetres = trip.Location?.AccuracyMetres,
            LocationCapturedOn = trip.Location?.CapturedOn.ToUniversalTime(),
            LocationSource = trip.Location?.Source,
            LocationVisibility = trip.Location?.Visibility,
            LocationConsentVersion = trip.Location?.ConsentVersion
        };
    }

    private async Task<Trip?> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid id,
        CancellationToken cancellationToken)
    {
        const string sql = $"""
            {SelectSql}
            WHERE "Id" = @Id;
            """;
        var row = await connection.QuerySingleOrDefaultAsync<TripPersistenceRow>(new CommandDefinition(
            sql,
            new { Id = id },
            transaction,
            cancellationToken: cancellationToken));
        return row is null ? null : _mapper.Map<Trip>(row);
    }

    private static TripStatusEnum ToStatus(string? status)
    {
        return Enum.TryParse<TripStatusEnum>(status, ignoreCase: false, out var parsed)
            ? parsed
            : TripStatusEnum.Completed;
    }

    public sealed class TripSummaryRow
    {
        public Guid Id { get; init; }

        public string? Status { get; init; }

        public DateTimeOffset StartedOn { get; init; }

        public DateTimeOffset? EndedOn { get; init; }

        public string? Title { get; init; }

        public string? PlaceName { get; init; }

        public int CatchCount { get; init; }

        public int PhotographCount { get; init; }

        public int NoteCount { get; init; }
    }

    public sealed class TripPersistenceRow
    {
        public Guid Id { get; init; }

        public Guid OwnerUserId { get; init; }

        public string? Title { get; init; }

        public string? PlaceName { get; init; }

        public string Status { get; init; } = string.Empty;

        public DateTimeOffset StartedOn { get; init; }

        public DateTimeOffset? EndedOn { get; init; }

        public double? Latitude { get; init; }

        public double? Longitude { get; init; }

        public double? LocationAccuracyMetres { get; init; }

        public DateTimeOffset? LocationCapturedOn { get; init; }

        public string? LocationSource { get; init; }

        public string? LocationVisibility { get; init; }

        public string? LocationConsentVersion { get; init; }

        public DateTimeOffset CreatedOn { get; init; }

        public DateTimeOffset UpdatedOn { get; init; }
    }

    private sealed class TripPersistenceParameters
    {
        public Guid Id { get; init; }

        public Guid OwnerUserId { get; init; }

        public string? Title { get; init; }

        public string? PlaceName { get; init; }

        public string Status { get; init; } = string.Empty;

        public DateTimeOffset StartedOn { get; init; }

        public DateTimeOffset? EndedOn { get; init; }

        public double? Latitude { get; init; }

        public double? Longitude { get; init; }

        public double? LocationAccuracyMetres { get; init; }

        public DateTimeOffset? LocationCapturedOn { get; init; }

        public string? LocationSource { get; init; }

        public string? LocationVisibility { get; init; }

        public string? LocationConsentVersion { get; init; }
    }
}
