using Dapper;
using FishingLogBook.Application.Trips.Contracts.Repositories;
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
            id,
            tripid,
            createdbyuserid,
            text,
            recordedon
        FROM tripnotes
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
                WHERE id = @Id;
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
                WHERE tripid = @TripId
                ORDER BY recordedon, id;
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
                INSERT INTO tripnotes (id, tripid, createdbyuserid, text, recordedon)
                VALUES (@Id, @TripId, @CreatedByUserId, @Text, @RecordedOn)
                ON CONFLICT (id) DO UPDATE SET
                    text = EXCLUDED.text,
                    recordedon = EXCLUDED.recordedon,
                    updatedon = now()
                WHERE tripnotes.tripid = EXCLUDED.tripid;
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
                """DELETE FROM tripnotes WHERE id = @Id;""",
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
