using Dapper;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Profiles.Errors;
using FishingLogBook.Domain.Profiles;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Infrastructure.Persistence.Repositories;

public sealed class ProfileRepository : IProfileRepository
{
    private const string FailedMessage = "Failed to load angler profile.";

    private const string SelectSql = """
        SELECT "UserId", "DisplayName", "PhotographId", "PhotographObjectKey", "PhotographContentType",
               "HomeRegion",
               "PreferredWeightUnit", "PreferredLengthUnit",
               "ShowDisplayName", "ShowPhotograph", "ShowHomeRegion",
               "ShowPreferredFishingMethods", "ShowPreferredSpecies", "OnboardingCompletedOn"
        FROM "Profile"
        """;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<ProfileRepository> _logger;

    public ProfileRepository(IDbConnectionFactory connectionFactory, ILogger<ProfileRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
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
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to check whether user {UserId} exists.", userId);
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
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load angler profile {UserId}.", userId);
            return Result.Fail<Profile?>(FailedMessage);
        }
    }

    public async Task<Result<IReadOnlyList<Profile>>> GetByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return Result.Ok<IReadOnlyList<Profile>>([]);
        }

        try
        {
            const string sql = $"""
                {SelectSql}
                WHERE "UserId" = ANY(@UserIds);
                """;
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var profiles = await connection.QueryAsync<Profile>(new CommandDefinition(
                sql,
                new { UserIds = userIds.ToArray() },
                cancellationToken: cancellationToken));
            return Result.Ok<IReadOnlyList<Profile>>([.. profiles]);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load {Count} angler profiles.", userIds.Count);
            return Result.Fail<IReadOnlyList<Profile>>(FailedMessage);
        }
    }

    public async Task<Result<IReadOnlyList<AnglerSummary>>> FindAnglersAsync(
        FindAnglersArgs args,
        CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                SELECT
                    u."Id" AS "UserId",
                    CASE WHEN p."ShowDisplayName" THEN p."DisplayName" END AS "DisplayName",
                    CASE WHEN p."ShowPhotograph" THEN p."PhotographObjectKey" END AS "PhotographObjectKey",
                    CASE WHEN p."ShowHomeRegion" THEN p."HomeRegion" END AS "HomeRegion"
                FROM "User" u
                LEFT JOIN "Profile" p ON p."UserId" = u."Id"
                WHERE u."Id" <> @RequestingUserId
                  AND (
                        (COALESCE(p."ShowDisplayName", false) AND p."DisplayName" ILIKE @NamePattern)
                     OR lower(u."Email") = lower(@Query)
                  )
                ORDER BY p."DisplayName", u."Id"
                LIMIT @MaxResults;
                """;
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var rows = await connection.QueryAsync<AnglerSummary>(new CommandDefinition(
                sql,
                new
                {
                    args.RequestingUserId,
                    args.Query,
                    NamePattern = ToNamePattern(args.Query),
                    args.MaxResults
                },
                cancellationToken: cancellationToken));
            return Result.Ok<IReadOnlyList<AnglerSummary>>([.. rows]);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to look up anglers for user {UserId}.", args.RequestingUserId);
            return Result.Fail<IReadOnlyList<AnglerSummary>>(FailedMessage);
        }
    }

    private static string ToNamePattern(string query)
    {
        var escaped = query
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }

    public async Task<Result<Profile>> UpsertAsync(Profile profile, CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                INSERT INTO "Profile" (
                    "UserId", "DisplayName", "PhotographId", "PhotographObjectKey", "PhotographContentType",
                    "HomeRegion",
                    "PreferredWeightUnit", "PreferredLengthUnit",
                    "ShowDisplayName", "ShowPhotograph", "ShowHomeRegion",
                    "ShowPreferredFishingMethods", "ShowPreferredSpecies",
                    "UpdatedOn")
                VALUES (
                    @UserId, @DisplayName, @PhotographId, @PhotographObjectKey, @PhotographContentType,
                    @HomeRegion,
                    @PreferredWeightUnit, @PreferredLengthUnit,
                    @ShowDisplayName, @ShowPhotograph, @ShowHomeRegion,
                    @ShowPreferredFishingMethods, @ShowPreferredSpecies,
                    now())
                ON CONFLICT ("UserId") DO UPDATE SET
                    "DisplayName" = EXCLUDED."DisplayName",
                    "HomeRegion" = EXCLUDED."HomeRegion",
                    "PreferredWeightUnit" = EXCLUDED."PreferredWeightUnit",
                    "PreferredLengthUnit" = EXCLUDED."PreferredLengthUnit",
                    "ShowDisplayName" = EXCLUDED."ShowDisplayName",
                    "ShowPhotograph" = EXCLUDED."ShowPhotograph",
                    "ShowHomeRegion" = EXCLUDED."ShowHomeRegion",
                    "ShowPreferredFishingMethods" = EXCLUDED."ShowPreferredFishingMethods",
                    "ShowPreferredSpecies" = EXCLUDED."ShowPreferredSpecies",
                    "UpdatedOn" = now();
                """;
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                ToParameters(profile),
                cancellationToken: cancellationToken));
            return await RequireByUserIdAsync(connection, profile.UserId, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to save angler profile {UserId}.", profile.UserId);
            return Result.Fail<Profile>(FailedMessage);
        }
    }

    public async Task<Result<Profile>> CompleteOnboardingAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                INSERT INTO "Profile" ("UserId", "OnboardingCompletedOn", "UpdatedOn")
                VALUES (@UserId, now(), now())
                ON CONFLICT ("UserId") DO UPDATE SET
                    "OnboardingCompletedOn" = COALESCE("Profile"."OnboardingCompletedOn", now()),
                    "UpdatedOn" = now();
                """;
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { UserId = userId },
                cancellationToken: cancellationToken));
            return await RequireByUserIdAsync(connection, userId, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to complete onboarding for user {UserId}.", userId);
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
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to update angler profile photograph {UserId}.", args.UserId);
            return Result.Fail<Profile>(FailedMessage);
        }
    }

    private static ProfilePersistenceParameters ToParameters(Profile profile)
    {
        return new ProfilePersistenceParameters
        {
            UserId = profile.UserId,
            DisplayName = profile.DisplayName,
            PhotographId = profile.PhotographId,
            PhotographObjectKey = profile.PhotographObjectKey,
            PhotographContentType = profile.PhotographContentType,
            HomeRegion = profile.HomeRegion,
            PreferredWeightUnit = (int)profile.PreferredWeightUnit,
            PreferredLengthUnit = (int)profile.PreferredLengthUnit,
            ShowDisplayName = profile.ShowDisplayName,
            ShowPhotograph = profile.ShowPhotograph,
            ShowHomeRegion = profile.ShowHomeRegion,
            ShowPreferredFishingMethods = profile.ShowPreferredFishingMethods,
            ShowPreferredSpecies = profile.ShowPreferredSpecies
        };
    }

    private sealed class ProfilePersistenceParameters
    {
        public Guid UserId { get; init; }

        public string? DisplayName { get; init; }

        public Guid? PhotographId { get; init; }

        public string? PhotographObjectKey { get; init; }

        public string? PhotographContentType { get; init; }

        public string? HomeRegion { get; init; }

        public int PreferredWeightUnit { get; init; }

        public int PreferredLengthUnit { get; init; }

        public bool ShowDisplayName { get; init; }

        public bool ShowPhotograph { get; init; }

        public bool ShowHomeRegion { get; init; }

        public bool ShowPreferredFishingMethods { get; init; }

        public bool ShowPreferredSpecies { get; init; }
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
