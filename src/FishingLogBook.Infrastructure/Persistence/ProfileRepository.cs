using Dapper;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Profiles.Errors;
using FishingLogBook.Domain.Profiles;
using FluentResults;

namespace FishingLogBook.Infrastructure.Persistence;

public sealed class ProfileRepository : IProfileRepository
{
    private const string FailedMessage = "Failed to load angler profile.";

    private const string SelectSql = """
        SELECT "UserId", "DisplayName", "PhotographId", "PhotographObjectKey", "PhotographContentType",
               "HomeRegion", "PreferredFishingTypes", "PreferredSpecies",
               "ShowDisplayName", "ShowPhotograph", "ShowHomeRegion",
               "ShowPreferredFishingTypes", "ShowPreferredSpecies"
        FROM "Profile"
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public ProfileRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Result<bool>> UserExistsAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """SELECT EXISTS (SELECT 1 FROM "User" WHERE "Id" = @UserId);""";
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                sql,
                new { UserId = userId },
                cancellationToken: cancellationToken));
            return Result.Ok(exists);
        }
        catch (Exception)
        {
            return Result.Fail<bool>(FailedMessage);
        }
    }

    public async Task<Result<Profile?>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            const string sql = $"""
                {SelectSql}
                WHERE "UserId" = @UserId;
                """;
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var profile = await connection.QuerySingleOrDefaultAsync<Profile>(new CommandDefinition(
                sql,
                new { UserId = userId },
                cancellationToken: cancellationToken));
            return Result.Ok(profile);
        }
        catch (Exception)
        {
            return Result.Fail<Profile?>(FailedMessage);
        }
    }

    public async Task<Result<Profile>> UpsertAsync(Profile profile, CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                INSERT INTO "Profile" (
                    "UserId", "DisplayName", "PhotographId", "PhotographObjectKey", "PhotographContentType",
                    "HomeRegion", "PreferredFishingTypes", "PreferredSpecies",
                    "ShowDisplayName", "ShowPhotograph", "ShowHomeRegion",
                    "ShowPreferredFishingTypes", "ShowPreferredSpecies",
                    "UpdatedOn")
                VALUES (
                    @UserId, @DisplayName, @PhotographId, @PhotographObjectKey, @PhotographContentType,
                    @HomeRegion, @PreferredFishingTypes, @PreferredSpecies,
                    @ShowDisplayName, @ShowPhotograph, @ShowHomeRegion,
                    @ShowPreferredFishingTypes, @ShowPreferredSpecies,
                    now())
                ON CONFLICT ("UserId") DO UPDATE SET
                    "DisplayName" = EXCLUDED."DisplayName",
                    "HomeRegion" = EXCLUDED."HomeRegion",
                    "PreferredFishingTypes" = EXCLUDED."PreferredFishingTypes",
                    "PreferredSpecies" = EXCLUDED."PreferredSpecies",
                    "ShowDisplayName" = EXCLUDED."ShowDisplayName",
                    "ShowPhotograph" = EXCLUDED."ShowPhotograph",
                    "ShowHomeRegion" = EXCLUDED."ShowHomeRegion",
                    "ShowPreferredFishingTypes" = EXCLUDED."ShowPreferredFishingTypes",
                    "ShowPreferredSpecies" = EXCLUDED."ShowPreferredSpecies",
                    "UpdatedOn" = now();
                """;
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(sql, profile, cancellationToken: cancellationToken));
            return await RequireByUserIdAsync(connection, profile.UserId, cancellationToken);
        }
        catch (Exception)
        {
            return Result.Fail<Profile>(FailedMessage);
        }
    }

    public async Task<Result<Profile>> UpdatePhotographAsync(
        RecordProfilePhotographArgs args,
        CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                UPDATE "Profile"
                SET "PhotographId" = @PhotographId,
                    "PhotographObjectKey" = @ObjectKey,
                    "PhotographContentType" = @ContentType,
                    "UpdatedOn" = now()
                WHERE "UserId" = @UserId;
                """;
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var updated = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                args,
                cancellationToken: cancellationToken));
            if (updated == 0)
            {
                return Result.Fail<Profile>(new ProfileNotFoundError());
            }

            return await RequireByUserIdAsync(connection, args.UserId, cancellationToken);
        }
        catch (Exception)
        {
            return Result.Fail<Profile>(FailedMessage);
        }
    }

    private static async Task<Result<Profile>> RequireByUserIdAsync(
        System.Data.IDbConnection connection,
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = $"""
            {SelectSql}
            WHERE "UserId" = @UserId;
            """;
        var profile = await connection.QuerySingleOrDefaultAsync<Profile>(new CommandDefinition(
            sql,
            new { UserId = userId },
            cancellationToken: cancellationToken));
        if (profile is null)
        {
            return Result.Fail<Profile>(new ProfileNotFoundError());
        }

        return Result.Ok(profile);
    }
}
