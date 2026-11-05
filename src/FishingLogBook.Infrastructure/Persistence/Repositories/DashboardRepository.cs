using System.Data;
using Dapper;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Dashboard.Contracts.Repositories;
using FishingLogBook.Domain.Dashboard;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Infrastructure.Persistence.Repositories;

public sealed class DashboardRepository(IDbConnectionFactory connectionFactory, ILogger<DashboardRepository> logger) : IDashboardRepository
{
    private const string Owned = """
        WITH owned AS (
            SELECT id, caughton, weight, tripid,
                NULLIF(btrim(speciesname), '') AS speciesname,
                NULLIF(btrim(method), '') AS method,
                NULLIF(btrim(placename), '') AS venue
            FROM catches
            WHERE caughtbyuserid = @UserId
        )
        """;

    private const string Filtered = """
        , filtered AS (
            SELECT * FROM owned
            WHERE (@FromInclusive::timestamptz IS NULL OR caughton >= @FromInclusive)
              AND (@ToExclusive::timestamptz IS NULL OR caughton < @ToExclusive)
              AND (@Species::text IS NULL OR speciesname = @Species)
              AND (@Method::text IS NULL OR method = @Method)
              AND (@Venue::text IS NULL OR venue = @Venue)
        )
        """;

    private const string PersonalBests = """
        SELECT DISTINCT ON (speciesname, method)
            speciesname, method, weight, id AS catchid, caughton
        FROM
        """;

    private const string BestOrder = """
        WHERE weight > 0
        ORDER BY speciesname NULLS LAST, method NULLS LAST, weight DESC, caughton, id;
        """;

    private const string Sql = Owned + """

        SELECT COUNT(*) AS catches, COUNT(DISTINCT speciesname) AS species,
            (SELECT COUNT(*) FROM trips t
             WHERE t.status IN ('Active', 'Completed')
               AND (t.owneruserid = @UserId OR EXISTS (
                   SELECT 1 FROM tripparticipants p
                   WHERE p.tripid = t.id AND p.userid = @UserId
                     AND p.status = 'Accepted' AND p.removedon IS NULL
               ))) AS trips
        FROM owned;

        """ + Owned + PersonalBests + " owned " + BestOrder
        + Owned + Filtered + """

        SELECT COUNT(*) AS catches, COUNT(DISTINCT tripid) AS trips,
            COUNT(DISTINCT speciesname) AS species
        FROM filtered;

        """ + Owned + Filtered + """

        SELECT speciesname, method, COUNT(*) AS count
        FROM filtered
        GROUP BY speciesname, method
        ORDER BY speciesname NULLS LAST, method NULLS LAST;

        """ + Owned + Filtered + PersonalBests + " filtered " + BestOrder
        + Owned + " SELECT DISTINCT speciesname FROM owned WHERE speciesname IS NOT NULL ORDER BY speciesname;"
        + Owned + " SELECT DISTINCT method FROM owned WHERE method IS NOT NULL ORDER BY method;"
        + Owned + " SELECT DISTINCT venue FROM owned WHERE venue IS NOT NULL ORDER BY venue;";

    public async Task<Result<DashboardSnapshot>> GetAsync(GetDashboardArgs args, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
            using var results = await connection.QueryMultipleAsync(new CommandDefinition(
                Sql, args, transaction, cancellationToken: cancellationToken));
            var snapshot = new DashboardSnapshot
            {
                Lifetime = await results.ReadSingleAsync<DashboardTotals>(),
                AllTimePersonalBests = (await results.ReadAsync<DashboardPersonalBest>()).AsList(),
                Filtered = await results.ReadSingleAsync<DashboardTotals>(),
                CatchStatistics = (await results.ReadAsync<DashboardCatchCount>()).AsList(),
                FilteredPersonalBests = (await results.ReadAsync<DashboardPersonalBest>()).AsList(),
                Species = (await results.ReadAsync<string>()).AsList(),
                Methods = (await results.ReadAsync<string>()).AsList(),
                Venues = (await results.ReadAsync<string>()).AsList()
            };
            await transaction.CommitAsync(cancellationToken);
            return Result.Ok(snapshot);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(exception, "Dashboard query cancelled.");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Dashboard could not be loaded for user {UserId}.", args.UserId);
            return Result.Fail<DashboardSnapshot>("Dashboard could not be loaded.");
        }
    }
}
