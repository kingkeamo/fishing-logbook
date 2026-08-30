using Dapper;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Capabilities.Contracts.Repositories;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Users;
using FluentResults;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FishingLogBook.Infrastructure.Persistence.Repositories;

public sealed class UserPlatformCapabilityRepository : IUserPlatformCapabilityRepository
{
    private const string FailedMessage = "Failed to persist platform capability.";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<UserPlatformCapabilityRepository> _logger;

    public UserPlatformCapabilityRepository(
        IDbConnectionFactory connectionFactory,
        ILogger<UserPlatformCapabilityRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Result<bool>> HasAsync(
        FindUserPlatformCapabilityArgs args,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            const string sql = """
                SELECT EXISTS (
                    SELECT 1
                    FROM "UserPlatformCapability"
                    WHERE "UserId" = @UserId AND "CapabilityCode" = @CapabilityCode);
                """;
            var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                sql,
                ToParameters(args.UserId, args.Capability),
                cancellationToken: cancellationToken));
            return Result.Ok(exists);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to check platform capability for user {UserId}.", args.UserId);
            return Result.Fail<bool>(FailedMessage);
        }
    }

    public async Task<Result<IReadOnlyList<PlatformCapabilityEnum>>> GetForUserAsync(
        FindUserPlatformCapabilitiesArgs args,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            const string sql = """
                SELECT "CapabilityCode"
                FROM "UserPlatformCapability"
                WHERE "UserId" = @UserId;
                """;
            var codes = await connection.QueryAsync<string>(new CommandDefinition(
                sql,
                new { args.UserId },
                cancellationToken: cancellationToken));
            return Result.Ok<IReadOnlyList<PlatformCapabilityEnum>>(
                codes.Select(ParseCapability).ToArray());
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load platform capabilities for user {UserId}.", args.UserId);
            return Result.Fail<IReadOnlyList<PlatformCapabilityEnum>>(FailedMessage);
        }
    }

    public async Task<Result> GrantAsync(UserPlatformCapability association, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            const string sql = """
                INSERT INTO "UserPlatformCapability" ("UserId", "CapabilityCode")
                VALUES (@UserId, @CapabilityCode)
                ON CONFLICT ("UserId", "CapabilityCode") DO NOTHING;
                """;
            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                ToParameters(association.UserId, association.Capability),
                cancellationToken: cancellationToken));
            return Result.Ok();
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            _logger.LogWarning(exception, "Platform capability grant failed for user {UserId}.", association.UserId);
            return Result.Fail("Platform capability is invalid.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to grant platform capability for user {UserId}.", association.UserId);
            return Result.Fail(FailedMessage);
        }
    }

    public async Task<Result> RevokeAsync(
        FindUserPlatformCapabilityArgs args,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            const string sql = """
                DELETE FROM "UserPlatformCapability"
                WHERE "UserId" = @UserId AND "CapabilityCode" = @CapabilityCode;
                """;
            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                ToParameters(args.UserId, args.Capability),
                cancellationToken: cancellationToken));
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to revoke platform capability for user {UserId}.", args.UserId);
            return Result.Fail(FailedMessage);
        }
    }

    private static UserPlatformCapabilityPersistenceParameters ToParameters(Guid userId, PlatformCapabilityEnum capability)
    {
        return new UserPlatformCapabilityPersistenceParameters
        {
            UserId = userId,
            CapabilityCode = capability.ToString()
        };
    }

    private static PlatformCapabilityEnum ParseCapability(string code)
    {
        return Enum.Parse<PlatformCapabilityEnum>(code);
    }

    private sealed class UserPlatformCapabilityPersistenceParameters
    {
        public Guid UserId { get; init; }

        public string CapabilityCode { get; init; } = string.Empty;
    }
}
