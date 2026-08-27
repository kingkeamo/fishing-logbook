using Dapper;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Domain.Trips;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Infrastructure.Persistence.Repositories;

public sealed class TripNoteRepository : ITripNoteRepository
{
    private const string FailedMessage = "Failed to save the trip note.";
    private const string LoadFailedMessage = "Failed to load the trip notes.";

    private const string SelectSql = """
        SELECT
            "Id",
            "TripId",
            "CreatedByUserId",
            "Text",
            "RecordedOn"
        FROM "TripNote"
        """;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<TripNoteRepository> _logger;

    public TripNoteRepository(
        IDbConnectionFactory connectionFactory,
        ILogger<TripNoteRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Result<TripNote?>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            const string sql = $"""
                {SelectSql}
                WHERE "Id" = @Id;
                """;
            var note = await connection.QuerySingleOrDefaultAsync<TripNote>(
                new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
            return Result.Ok(note);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load the trip note {NoteId}.", id);
            return Result.Fail<TripNote?>(LoadFailedMessage);
        }
    }

    public async Task<Result<IReadOnlyList<TripNote>>> GetByTripIdAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            const string sql = $"""
                {SelectSql}
                WHERE "TripId" = @TripId
                ORDER BY "RecordedOn", "Id";
                """;
            var notes = await connection.QueryAsync<TripNote>(
                new CommandDefinition(sql, new { TripId = tripId }, cancellationToken: cancellationToken));
            return Result.Ok<IReadOnlyList<TripNote>>([.. notes]);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load notes for trip {TripId}.", tripId);
            return Result.Fail<IReadOnlyList<TripNote>>(LoadFailedMessage);
        }
    }

    public async Task<Result<TripNote>> UpsertAsync(TripNote note, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            const string sql = """
                INSERT INTO "TripNote" ("Id", "TripId", "CreatedByUserId", "Text", "RecordedOn")
                VALUES (@Id, @TripId, @CreatedByUserId, @Text, @RecordedOn)
                ON CONFLICT ("Id") DO UPDATE SET
                    "Text" = EXCLUDED."Text",
                    "RecordedOn" = EXCLUDED."RecordedOn",
                    "UpdatedOn" = now()
                WHERE "TripNote"."TripId" = EXCLUDED."TripId";
                """;
            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                ToParameters(note),
                cancellationToken: cancellationToken));
            var saved = await GetByIdAsync(note.Id, cancellationToken);
            if (saved.IsFailed)
            {
                return Result.Fail<TripNote>(saved.Errors);
            }

            return saved.Value is null
                ? Result.Fail<TripNote>(FailedMessage)
                : Result.Ok(saved.Value);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to save the note {NoteId} for trip {TripId}.",
                note.Id,
                note.TripId);
            return Result.Fail<TripNote>(FailedMessage);
        }
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                """DELETE FROM "TripNote" WHERE "Id" = @Id;""",
                new { Id = id },
                cancellationToken: cancellationToken));
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to delete the trip note {NoteId}.", id);
            return Result.Fail(FailedMessage);
        }
    }

    private static TripNotePersistenceParameters ToParameters(TripNote note)
    {
        return new TripNotePersistenceParameters
        {
            Id = note.Id,
            TripId = note.TripId,
            CreatedByUserId = note.CreatedByUserId,
            Text = note.Text,
            RecordedOn = note.RecordedOn.ToUniversalTime()
        };
    }

    private sealed class TripNotePersistenceParameters
    {
        public Guid Id { get; init; }

        public Guid TripId { get; init; }

        public Guid CreatedByUserId { get; init; }

        public string Text { get; init; } = string.Empty;

        public DateTimeOffset RecordedOn { get; init; }
    }
}
