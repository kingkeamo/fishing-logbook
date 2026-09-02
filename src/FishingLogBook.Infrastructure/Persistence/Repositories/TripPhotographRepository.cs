using Dapper;
using FishingLogBook.Application.Trips.Contracts.Repositories;
using FishingLogBook.Domain.Trips;
using FluentResults;
using MapsterMapper;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Infrastructure.Persistence.Repositories;

public sealed class TripPhotographRepository : ITripPhotographRepository
{
    private const string FailedMessage = "Failed to save the trip photograph.";
    private const string LoadFailedMessage = "Failed to load the trip photographs.";

    private const string SelectSql = """
        SELECT
            id,
            tripid,
            contributedbyuserid,
            objectkey,
            contenttype,
            capturedon,
            addedon
        FROM tripphotographs
        """;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<TripPhotographRepository> _logger;
    private readonly IMapper _mapper;

    public TripPhotographRepository(
        IDbConnectionFactory connectionFactory,
        ILogger<TripPhotographRepository> logger,
        IMapper mapper)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _mapper = mapper;
    }

    public async Task<Result<TripPhotograph?>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            const string sql = $"""
                {SelectSql}
                WHERE id = @Id;
                """;
            var row = await connection.QuerySingleOrDefaultAsync<TripPhotographPersistenceRow>(
                new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
            return Result.Ok(row is null ? null : _mapper.Map<TripPhotograph>(row));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load the trip photograph {PhotographId}.", id);
            return Result.Fail<TripPhotograph?>(LoadFailedMessage);
        }
    }

    public async Task<Result<IReadOnlyList<TripPhotograph>>> GetByTripIdAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            const string sql = $"""
                {SelectSql}
                WHERE tripid = @TripId
                ORDER BY COALESCE(capturedon, addedon), id;
                """;
            var rows = await connection.QueryAsync<TripPhotographPersistenceRow>(
                new CommandDefinition(sql, new { TripId = tripId }, cancellationToken: cancellationToken));
            return Result.Ok<IReadOnlyList<TripPhotograph>>(
                [.. rows.Select(row => _mapper.Map<TripPhotograph>(row))]);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load photographs for trip {TripId}.", tripId);
            return Result.Fail<IReadOnlyList<TripPhotograph>>(LoadFailedMessage);
        }
    }

    public async Task<Result<TripPhotograph>> UpsertAsync(
        TripPhotograph photograph,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            const string sql = """
                INSERT INTO tripphotographs (
                    id,
                    tripid,
                    contributedbyuserid,
                    objectkey,
                    contenttype,
                    capturedon,
                    addedon)
                VALUES (
                    @Id,
                    @TripId,
                    @ContributedByUserId,
                    @ObjectKey,
                    @ContentType,
                    @CapturedOn,
                    @AddedOn)
                ON CONFLICT (id) DO UPDATE SET
                    objectkey = EXCLUDED.objectkey,
                    contenttype = EXCLUDED.contenttype,
                    capturedon = EXCLUDED.capturedon,
                    addedon = EXCLUDED.addedon,
                    updatedon = now()
                WHERE tripphotographs.tripid = EXCLUDED.tripid;
                """;
            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                ToParameters(photograph),
                cancellationToken: cancellationToken));
            var saved = await GetByIdAsync(photograph.Id, cancellationToken);
            if (saved.IsFailed)
            {
                return Result.Fail<TripPhotograph>(saved.Errors);
            }

            return saved.Value is null
                ? Result.Fail<TripPhotograph>(FailedMessage)
                : Result.Ok(saved.Value);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to save the photograph {PhotographId} for trip {TripId}.",
                photograph.Id,
                photograph.TripId);
            return Result.Fail<TripPhotograph>(FailedMessage);
        }
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                """DELETE FROM tripphotographs WHERE id = @Id;""",
                new { Id = id },
                cancellationToken: cancellationToken));
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to delete the trip photograph {PhotographId}.", id);
            return Result.Fail(FailedMessage);
        }
    }

    private static TripPhotographPersistenceParameters ToParameters(TripPhotograph photograph)
    {
        return new TripPhotographPersistenceParameters
        {
            Id = photograph.Id,
            TripId = photograph.TripId,
            ContributedByUserId = photograph.ContributedByUserId,
            ObjectKey = photograph.ObjectKey,
            ContentType = photograph.ContentType,
            CapturedOn = photograph.CapturedOn?.ToUniversalTime(),
            AddedOn = photograph.AddedOn.ToUniversalTime()
        };
    }

    internal sealed class TripPhotographPersistenceRow
    {
        public Guid Id { get; init; }

        public Guid TripId { get; init; }

        public Guid ContributedByUserId { get; init; }

        public string ObjectKey { get; init; } = string.Empty;

        public string ContentType { get; init; } = string.Empty;

        public DateTimeOffset? CapturedOn { get; init; }

        public DateTimeOffset AddedOn { get; init; }
    }

    private sealed class TripPhotographPersistenceParameters
    {
        public Guid Id { get; init; }

        public Guid TripId { get; init; }

        public Guid ContributedByUserId { get; init; }

        public string ObjectKey { get; init; } = string.Empty;

        public string ContentType { get; init; } = string.Empty;

        public DateTimeOffset? CapturedOn { get; init; }

        public DateTimeOffset AddedOn { get; init; }
    }
}
