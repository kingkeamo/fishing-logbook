using Dapper;
using FishingLogBook.Application.OfflineAccess.Contracts.Repositories;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Infrastructure.Persistence.Repositories;

public sealed class OfflineAccessPreferenceRepository : IOfflineAccessPreferenceRepository
{
    private const string FailureMessage = "Failed to update offline access preference.";
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<OfflineAccessPreferenceRepository> _logger;

    public OfflineAccessPreferenceRepository(IDbConnectionFactory connectionFactory, ILogger<OfflineAccessPreferenceRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Result<OfflineAccessPreferenceDto>> GetAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            const string sql = """
                SELECT "OfflineAccessEnabled" AS "Enabled",
                       "OfflineAccessEnabledAt" AS "EnabledAt"
                FROM "User"
                WHERE "Id" = @UserId;
                """;
            var value = await connection.QuerySingleOrDefaultAsync<OfflineAccessPreferenceDto>(
                new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));
            return value is not null
                ? Result.Ok(value)
                : Result.Fail<OfflineAccessPreferenceDto>(FailureMessage);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to get offline access preference for user {UserId}.", userId);
            return Result.Fail<OfflineAccessPreferenceDto>(FailureMessage);
        }
    }

    public async Task<Result<OfflineAccessPreferenceDto>> SetAsync(
        Guid userId,
        bool enabled,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            const string sql = """
                UPDATE "User"
                SET "OfflineAccessEnabled" = @Enabled,
                    "OfflineAccessEnabledAt" = CASE
                        WHEN @Enabled THEN CURRENT_TIMESTAMP
                        ELSE "OfflineAccessEnabledAt"
                    END
                WHERE "Id" = @UserId
                RETURNING "OfflineAccessEnabled" AS "Enabled",
                          "OfflineAccessEnabledAt" AS "EnabledAt";
                """;
            var value = await connection.QuerySingleOrDefaultAsync<OfflineAccessPreferenceDto>(
                new CommandDefinition(
                    sql,
                    new { UserId = userId, Enabled = enabled },
                    cancellationToken: cancellationToken));
            return value is not null
                ? Result.Ok(value)
                : Result.Fail<OfflineAccessPreferenceDto>(FailureMessage);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to set offline access preference for user {UserId}.", userId);
            return Result.Fail<OfflineAccessPreferenceDto>(FailureMessage);
        }
    }
}
