using Dapper;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Domain.Catalogue;
using FluentResults;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FishingLogBook.Infrastructure.Persistence;

public sealed class FishingPreferenceRepository : IFishingPreferenceRepository
{
    private const string LoadMethodsFailedMessage = "Failed to load fishing method preferences.";
    private const string LoadSpeciesFailedMessage = "Failed to load fishing species preferences.";
    private const string SaveFailedMessage = "Failed to save fishing preferences.";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<FishingPreferenceRepository> _logger;

    public FishingPreferenceRepository(
        IDbConnectionFactory connectionFactory,
        ILogger<FishingPreferenceRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<UserFishingMethodPreference>>> GetMethodPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                SELECT "UserId", "FishingMethodId", "IsDefault", "CreatedOn"
                FROM "UserFishingMethodPreference"
                WHERE "UserId" = @UserId;
                """;
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var rows = await connection.QueryAsync<UserFishingMethodPreference>(new CommandDefinition(
                sql,
                new { UserId = userId },
                cancellationToken: cancellationToken));
            return Result.Ok<IReadOnlyList<UserFishingMethodPreference>>([.. rows]);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load fishing method preferences for user {UserId}.", userId);
            return Result.Fail<IReadOnlyList<UserFishingMethodPreference>>(LoadMethodsFailedMessage);
        }
    }

    public async Task<Result<IReadOnlyList<UserFishingSpeciesPreference>>> GetSpeciesPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                SELECT "UserId", "FishingMethodId", "SpeciesId", "IsDefault", "CreatedOn"
                FROM "UserFishingSpeciesPreference"
                WHERE "UserId" = @UserId;
                """;
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var rows = await connection.QueryAsync<UserFishingSpeciesPreference>(new CommandDefinition(
                sql,
                new { UserId = userId },
                cancellationToken: cancellationToken));
            return Result.Ok<IReadOnlyList<UserFishingSpeciesPreference>>([.. rows]);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load fishing species preferences for user {UserId}.", userId);
            return Result.Fail<IReadOnlyList<UserFishingSpeciesPreference>>(LoadSpeciesFailedMessage);
        }
    }

    public async Task<Result> ReplacePreferencesAsync(
        Guid userId,
        IReadOnlyList<UserFishingMethodPreference> methods,
        IReadOnlyList<UserFishingSpeciesPreference> species,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await DeletePreferencesAsync(connection, transaction, userId, cancellationToken);
                await InsertMethodsAsync(connection, transaction, methods, cancellationToken);
                await InsertSpeciesAsync(connection, transaction, species, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Ok();
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to replace fishing preferences for user {UserId}.", userId);
            return Result.Fail(SaveFailedMessage);
        }
    }

    private static async Task DeletePreferencesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """DELETE FROM "UserFishingSpeciesPreference" WHERE "UserId" = @UserId;""",
            new { UserId = userId },
            transaction: transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """DELETE FROM "UserFishingMethodPreference" WHERE "UserId" = @UserId;""",
            new { UserId = userId },
            transaction: transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task InsertMethodsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<UserFishingMethodPreference> methods,
        CancellationToken cancellationToken)
    {
        if (methods.Count == 0)
        {
            return;
        }

        const string sql = """
            INSERT INTO "UserFishingMethodPreference" ("UserId", "FishingMethodId", "IsDefault", "CreatedOn")
            VALUES (@UserId, @FishingMethodId, @IsDefault, @CreatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            methods,
            transaction: transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task InsertSpeciesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<UserFishingSpeciesPreference> species,
        CancellationToken cancellationToken)
    {
        if (species.Count == 0)
        {
            return;
        }

        const string sql = """
            INSERT INTO "UserFishingSpeciesPreference" ("UserId", "FishingMethodId", "SpeciesId", "IsDefault", "CreatedOn")
            VALUES (@UserId, @FishingMethodId, @SpeciesId, @IsDefault, @CreatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            species,
            transaction: transaction,
            cancellationToken: cancellationToken));
    }
}
