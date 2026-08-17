using Dapper;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Domain.Catches;
using FluentResults;
using Npgsql;

namespace FishingLogBook.Infrastructure.Persistence;

public sealed class CatchRepository : ICatchRepository
{
    private const string FailedMessage = "Failed to save the catch.";

    private readonly IDbConnectionFactory _connectionFactory;

    public CatchRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Result<Catch?>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            return Result.Ok(await LoadAsync(connection, transaction: null, id, cancellationToken));
        }
        catch (Exception)
        {
            return Result.Fail<Catch?>(FailedMessage);
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
                await ReplacePhotographsAsync(connection, transaction, catchRecord, cancellationToken);
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
        catch (Exception)
        {
            return Result.Fail<Catch>(FailedMessage);
        }
    }

    private static async Task UpsertCatchRowAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Catch catchRecord,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO "Catch" ("Id", "UserId", "CaughtOn")
            VALUES (@Id, @UserId, @CaughtOn)
            ON CONFLICT ("Id") DO UPDATE SET
                "CaughtOn" = EXCLUDED."CaughtOn"
            WHERE "Catch"."UserId" = EXCLUDED."UserId";
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { catchRecord.Id, catchRecord.UserId, catchRecord.CaughtOn },
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

    private static async Task<Catch?> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid id,
        CancellationToken cancellationToken)
    {
        const string catchSql = """
            SELECT "Id", "UserId", "CaughtOn"
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

        return new Catch
        {
            Id = catchRow.Id,
            UserId = catchRow.UserId,
            CaughtOn = catchRow.CaughtOn,
            Photographs = photographs.ToArray()
        };
    }

    private sealed class CatchRow
    {
        public Guid Id { get; init; }

        public Guid UserId { get; init; }

        public DateTimeOffset CaughtOn { get; init; }
    }
}
