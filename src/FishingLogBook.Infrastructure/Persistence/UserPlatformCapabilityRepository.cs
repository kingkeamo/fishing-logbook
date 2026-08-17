using Dapper;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Users;
using FluentResults;
using Npgsql;

namespace FishingLogBook.Infrastructure.Persistence;

public sealed class UserPlatformCapabilityRepository : IUserPlatformCapabilityRepository
{
    private const string FailedMessage = "Failed to persist platform capability.";

    private readonly IDbConnectionFactory _connectionFactory;

    public UserPlatformCapabilityRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
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
        catch (Exception)
        {
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
        catch (Exception)
        {
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
            return Result.Fail("Platform capability is invalid.");
        }
        catch (Exception)
        {
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
        catch (Exception)
        {
            return Result.Fail(FailedMessage);
        }
    }

    private static object ToParameters(Guid userId, PlatformCapabilityEnum capability)
    {
        return new
        {
            UserId = userId,
            CapabilityCode = capability.ToString()
        };
    }

    private static PlatformCapabilityEnum ParseCapability(string code)
    {
        return Enum.Parse<PlatformCapabilityEnum>(code);
    }
}
