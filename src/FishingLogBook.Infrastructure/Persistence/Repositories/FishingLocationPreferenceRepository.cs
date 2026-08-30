using Dapper;
using FishingLogBook.Application.FishingLocations.Contracts.Repositories;
using FishingLogBook.Application.FishingLocations.Errors;
using FishingLogBook.Domain.FishingLocations;
using FluentResults;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FishingLogBook.Infrastructure.Persistence.Repositories;

public sealed class FishingLocationPreferenceRepository : IFishingLocationPreferenceRepository
{
    private const string LoadFailedMessage = "Failed to load fishing locations.";
    private const string SaveFailedMessage = "Failed to save fishing locations.";
    private const string DuplicateMessage = "A fishing location with that name is already saved.";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<FishingLocationPreferenceRepository> _logger;

    public FishingLocationPreferenceRepository(
        IDbConnectionFactory connectionFactory,
        ILogger<FishingLocationPreferenceRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<UserFishingLocationPreference>>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                SELECT "Id", "UserId", "Name", "IsDefault", "CreatedOn"
                FROM "UserFishingLocationPreference"
                WHERE "UserId" = @UserId
                ORDER BY "IsDefault" DESC, lower("Name");
                """;
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var rows = await connection.QueryAsync<UserFishingLocationPreference>(new CommandDefinition(
                sql,
                new { UserId = userId },
                cancellationToken: cancellationToken));
            return Result.Ok<IReadOnlyList<UserFishingLocationPreference>>([.. rows]);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load fishing locations for user {UserId}.", userId);
            return Result.Fail<IReadOnlyList<UserFishingLocationPreference>>(LoadFailedMessage);
        }
    }

    public async Task<Result> ReplaceAsync(
        Guid userId,
        IReadOnlyList<UserFishingLocationPreference> locations,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await DeleteAsync(connection, transaction, userId, cancellationToken);
                await InsertAsync(connection, transaction, locations, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Ok();
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
                "Rejected duplicate fishing locations for user {UserId}.",
                userId);
            return Result.Fail(new DuplicateFishingLocationError(DuplicateMessage));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to save fishing locations for user {UserId}.", userId);
            return Result.Fail(SaveFailedMessage);
        }
    }

    private static async Task DeleteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """DELETE FROM "UserFishingLocationPreference" WHERE "UserId" = @UserId;""",
            new { UserId = userId },
            transaction: transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task InsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<UserFishingLocationPreference> locations,
        CancellationToken cancellationToken)
    {
        if (locations.Count == 0)
        {
            return;
        }

        const string sql = """
            INSERT INTO "UserFishingLocationPreference" ("Id", "UserId", "Name", "IsDefault", "CreatedOn")
            VALUES (@Id, @UserId, @Name, @IsDefault, @CreatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            locations,
            transaction: transaction,
            cancellationToken: cancellationToken));
    }
}
