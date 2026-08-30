using Dapper;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Contracts.Repositories;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FluentResults;
using MapsterMapper;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Infrastructure.Persistence.Repositories;

public sealed class TripParticipantRepository : ITripParticipantRepository
{
    private const string FailedMessage = "Failed to save the trip participant.";

    private const string SelectSql = """
        SELECT
            "Id",
            "TripId",
            "UserId",
            "Status",
            "InvitedByUserId",
            "InvitedOn",
            "RespondedOn",
            "RemovedOn"
        FROM "TripParticipant"
        """;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<TripParticipantRepository> _logger;
    private readonly IMapper _mapper;

    public TripParticipantRepository(
        IDbConnectionFactory connectionFactory,
        ILogger<TripParticipantRepository> logger,
        IMapper mapper)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _mapper = mapper;
    }

    public async Task<Result<TripParticipant?>> FindAsync(
        FindTripParticipantArgs args,
        CancellationToken cancellationToken)
    {
        try
        {
            const string sql = $"""
                {SelectSql}
                WHERE "TripId" = @TripId
                  AND "UserId" = @UserId;
                """;
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var row = await connection.QuerySingleOrDefaultAsync<TripParticipantPersistenceRow>(
                new CommandDefinition(
                    sql,
                    new { args.TripId, args.UserId },
                    cancellationToken: cancellationToken));
            return Result.Ok(row is null ? null : _mapper.Map<TripParticipant>(row));
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to load the participant of trip {TripId}.",
                args.TripId);
            return Result.Fail<TripParticipant?>(FailedMessage);
        }
    }

    public async Task<Result<IReadOnlyList<TripParticipant>>> GetByTripIdAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        try
        {
            const string sql = $"""
                {SelectSql}
                WHERE "TripId" = @TripId
                ORDER BY "InvitedOn", "UserId";
                """;
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var rows = await connection.QueryAsync<TripParticipantPersistenceRow>(new CommandDefinition(
                sql,
                new { TripId = tripId },
                cancellationToken: cancellationToken));
            return Result.Ok<IReadOnlyList<TripParticipant>>(
                [.. rows.Select(_mapper.Map<TripParticipant>)]);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load the participants of trip {TripId}.", tripId);
            return Result.Fail<IReadOnlyList<TripParticipant>>(FailedMessage);
        }
    }

    public async Task<Result<IReadOnlyList<TripParticipant>>> GetPendingInvitationsByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            const string sql = $"""
                {SelectSql}
                WHERE "UserId" = @UserId
                  AND "Status" = 'Pending'
                  AND "RemovedOn" IS NULL
                ORDER BY "InvitedOn" DESC, "TripId";
                """;
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var rows = await connection.QueryAsync<TripParticipantPersistenceRow>(new CommandDefinition(
                sql,
                new { UserId = userId },
                cancellationToken: cancellationToken));
            return Result.Ok<IReadOnlyList<TripParticipant>>(
                [.. rows.Select(_mapper.Map<TripParticipant>)]);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load trip invitations for user {UserId}.", userId);
            return Result.Fail<IReadOnlyList<TripParticipant>>(FailedMessage);
        }
    }

    public async Task<Result<TripParticipant>> UpsertAsync(
        TripParticipant participant,
        CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                INSERT INTO "TripParticipant" (
                    "Id",
                    "TripId",
                    "UserId",
                    "Status",
                    "InvitedByUserId",
                    "InvitedOn",
                    "RespondedOn",
                    "RemovedOn",
                    "UpdatedOn"
                )
                VALUES (
                    @Id,
                    @TripId,
                    @UserId,
                    @Status,
                    @InvitedByUserId,
                    @InvitedOn,
                    @RespondedOn,
                    @RemovedOn,
                    now()
                )
                ON CONFLICT ("TripId", "UserId") DO UPDATE SET
                    "Status" = EXCLUDED."Status",
                    "InvitedByUserId" = EXCLUDED."InvitedByUserId",
                    "InvitedOn" = EXCLUDED."InvitedOn",
                    "RespondedOn" = EXCLUDED."RespondedOn",
                    "RemovedOn" = EXCLUDED."RemovedOn",
                    "UpdatedOn" = now();
                """;
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                ToParameters(participant),
                cancellationToken: cancellationToken));
            var saved = await FindAsync(
                new FindTripParticipantArgs
                {
                    TripId = participant.TripId,
                    UserId = participant.UserId
                },
                cancellationToken);
            if (saved.IsFailed)
            {
                return Result.Fail<TripParticipant>(saved.Errors);
            }

            return saved.Value is null
                ? Result.Fail<TripParticipant>(FailedMessage)
                : Result.Ok(saved.Value);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to save the participant of trip {TripId}.",
                participant.TripId);
            return Result.Fail<TripParticipant>(FailedMessage);
        }
    }

    private static TripParticipantPersistenceParameters ToParameters(TripParticipant participant)
    {
        return new TripParticipantPersistenceParameters
        {
            Id = participant.Id,
            TripId = participant.TripId,
            UserId = participant.UserId,
            Status = participant.Status.ToString(),
            InvitedByUserId = participant.InvitedByUserId,
            InvitedOn = participant.InvitedOn.ToUniversalTime(),
            RespondedOn = participant.RespondedOn?.ToUniversalTime(),
            RemovedOn = participant.RemovedOn?.ToUniversalTime()
        };
    }

    public sealed class TripParticipantPersistenceRow
    {
        public Guid Id { get; init; }

        public Guid TripId { get; init; }

        public Guid UserId { get; init; }

        public string Status { get; init; } = string.Empty;

        public Guid InvitedByUserId { get; init; }

        public DateTimeOffset InvitedOn { get; init; }

        public DateTimeOffset? RespondedOn { get; init; }

        public DateTimeOffset? RemovedOn { get; init; }
    }

    private sealed class TripParticipantPersistenceParameters
    {
        public Guid Id { get; init; }

        public Guid TripId { get; init; }

        public Guid UserId { get; init; }

        public string Status { get; init; } = string.Empty;

        public Guid InvitedByUserId { get; init; }

        public DateTimeOffset InvitedOn { get; init; }

        public DateTimeOffset? RespondedOn { get; init; }

        public DateTimeOffset? RemovedOn { get; init; }
    }
}
