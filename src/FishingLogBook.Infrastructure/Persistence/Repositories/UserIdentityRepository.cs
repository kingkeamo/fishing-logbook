using Dapper;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Users.Contracts.Repositories;
using FishingLogBook.Domain.Users;
using FluentResults;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FishingLogBook.Infrastructure.Persistence.Repositories;

public sealed class UserIdentityRepository : IUserIdentityRepository
{
    private const string ResolveFailedMessage = "Failed to resolve FishingLogBook user.";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<UserIdentityRepository> _logger;

    public UserIdentityRepository(IDbConnectionFactory connectionFactory, ILogger<UserIdentityRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Result<Guid?>> FindUserIdAsync(
        FindUserIdentityArgs args,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var userId = await FindUserIdAsync(connection, args, transaction: null, cancellationToken);
            return Result.Ok(userId);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to find FishingLogBook user identity.");
            return Result.Fail<Guid?>(ResolveFailedMessage);
        }
    }

    public async Task<Result<Guid>> CreateAsync(
        User user,
        UserIdentity identity,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await InsertUserAndIdentityAsync(connection, transaction, user, identity, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Ok(user.Id);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogWarning(exception, "User identity already exists; recovering the existing user.");
                return await ExistingUserIdOrFailAsync(connection, user, identity, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to create FishingLogBook user identity.");
            return Result.Fail<Guid>(ResolveFailedMessage);
        }
    }

    public async Task<Result> UpdateEmailAsync(User user, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            await UpdateEmailAsync(connection, user, transaction: null, cancellationToken);
            return Result.Ok();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to update FishingLogBook user email.");
            return Result.Fail(ResolveFailedMessage);
        }
    }

    private static async Task InsertUserAndIdentityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        User user,
        UserIdentity identity,
        CancellationToken cancellationToken)
    {
        const string insertUserSql = """
            INSERT INTO "User" ("Id", "Email")
            VALUES (@Id, @Email);
            """;
        const string insertIdentitySql = """
            INSERT INTO "UserIdentity" ("Id", "UserId", "Provider", "Subject")
            VALUES (@Id, @UserId, @Provider, @Subject);
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            insertUserSql,
            user,
            transaction,
            cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            insertIdentitySql,
            identity,
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<Result<Guid>> ExistingUserIdOrFailAsync(
        NpgsqlConnection connection,
        User user,
        UserIdentity identity,
        CancellationToken cancellationToken)
    {
        var existing = await FindUserIdAsync(
            connection,
            new FindUserIdentityArgs
            {
                Provider = identity.Provider,
                Subject = identity.Subject
            },
            transaction: null,
            cancellationToken);
        if (existing is Guid existingUserId && existingUserId != Guid.Empty)
        {
            await UpdateEmailAsync(
                connection,
                new User { Id = existingUserId, Email = user.Email },
                transaction: null,
                cancellationToken);
            return Result.Ok(existingUserId);
        }

        return Result.Fail<Guid>(ResolveFailedMessage);
    }

    private static async Task UpdateEmailAsync(
        NpgsqlConnection connection,
        User user,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE "User"
            SET "Email" = @Email
            WHERE "Id" = @Id AND "Email" IS DISTINCT FROM @Email;
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            user,
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<Guid?> FindUserIdAsync(
        NpgsqlConnection connection,
        FindUserIdentityArgs args,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT "UserId"
            FROM "UserIdentity"
            WHERE "Provider" = @Provider AND "Subject" = @Subject;
            """;

        var command = new CommandDefinition(
            sql,
            args,
            transaction,
            cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(command);
    }
}
