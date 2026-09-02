using Dapper;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Profiles.Contracts.Repositories;
using FishingLogBook.Application.Profiles.Errors;
using FishingLogBook.Domain.Profiles;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Infrastructure.Persistence.Repositories;

public sealed class ProfileRepository : IProfileRepository
{
    private const string FailedMessage = "Failed to load angler profile.";

    private const string SelectSql = """
        SELECT userid, displayname, photographid, photographobjectkey, photographcontenttype,
               homeregion,
               preferredweightunit, preferredlengthunit,
               showdisplayname, showphotograph, showhomeregion,
               showpreferredfishingmethods, showpreferredspecies, onboardingcompletedon
        FROM profiles
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
            const string sql = """SELECT EXISTS (SELECT 1 FROM users WHERE id = @UserId);""";
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
                WHERE userid = @UserId;
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
                WHERE userid = ANY(@UserIds);
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
                    u.id AS userid,
                    CASE WHEN p.showdisplayname THEN p.displayname END AS displayname,
                    CASE WHEN p.showphotograph THEN p.photographobjectkey END AS photographobjectkey,
                    CASE WHEN p.showhomeregion THEN p.homeregion END AS homeregion,
                    u.email AS email
                FROM users u
                LEFT JOIN profiles p ON p.userid = u.id
                WHERE u.id <> @RequestingUserId
                  AND (
                        (COALESCE(p.showdisplayname, false) AND p.displayname ILIKE @SearchPattern)
                     OR u.email ILIKE @SearchPattern
                  )
                ORDER BY p.displayname, u.id
                LIMIT @MaxResults;
                """;
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var rows = await connection.QueryAsync<AnglerSummary>(new CommandDefinition(
                sql,
                new
                {
                    args.RequestingUserId,
                    SearchPattern = ToSearchPattern(args.Query),
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

    private static string ToSearchPattern(string query)
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
                INSERT INTO profiles (
                    userid, displayname, photographid, photographobjectkey, photographcontenttype,
                    homeregion,
                    preferredweightunit, preferredlengthunit,
                    showdisplayname, showphotograph, showhomeregion,
                    showpreferredfishingmethods, showpreferredspecies,
                    updatedon)
                VALUES (
                    @UserId, @DisplayName, @PhotographId, @PhotographObjectKey, @PhotographContentType,
                    @HomeRegion,
                    @PreferredWeightUnit, @PreferredLengthUnit,
                    @ShowDisplayName, @ShowPhotograph, @ShowHomeRegion,
                    @ShowPreferredFishingMethods, @ShowPreferredSpecies,
                    now())
                ON CONFLICT (userid) DO UPDATE SET
                    displayname = EXCLUDED.displayname,
                    homeregion = EXCLUDED.homeregion,
                    preferredweightunit = EXCLUDED.preferredweightunit,
                    preferredlengthunit = EXCLUDED.preferredlengthunit,
                    showdisplayname = EXCLUDED.showdisplayname,
                    showphotograph = EXCLUDED.showphotograph,
                    showhomeregion = EXCLUDED.showhomeregion,
                    showpreferredfishingmethods = EXCLUDED.showpreferredfishingmethods,
                    showpreferredspecies = EXCLUDED.showpreferredspecies,
                    updatedon = now();
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
                INSERT INTO profiles (userid, onboardingcompletedon, updatedon)
                VALUES (@UserId, now(), now())
                ON CONFLICT (userid) DO UPDATE SET
                    onboardingcompletedon = COALESCE(profiles.onboardingcompletedon, now()),
                    updatedon = now();
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
                UPDATE profiles
                SET photographid = @PhotographId,
                    photographobjectkey = @ObjectKey,
                    photographcontenttype = @ContentType,
                    updatedon = now()
                WHERE userid = @UserId;
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
            WHERE userid = @UserId;
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
