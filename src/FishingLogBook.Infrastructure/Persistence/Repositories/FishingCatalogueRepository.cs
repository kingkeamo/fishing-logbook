using Dapper;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Domain.Catalogue;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Infrastructure.Persistence.Repositories;

public sealed class FishingCatalogueRepository : IFishingCatalogueRepository
{
    private const string MethodsFailedMessage = "Failed to load fishing method catalogue.";
    private const string SpeciesFailedMessage = "Failed to load species catalogue.";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<FishingCatalogueRepository> _logger;

    public FishingCatalogueRepository(
        IDbConnectionFactory connectionFactory,
        ILogger<FishingCatalogueRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<FishingMethod>>> GetAllMethodsAsync(CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                SELECT "Id", "Code", "Name", "CreatedOn"
                FROM "FishingMethod"
                ORDER BY "Name";
                """;
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var rows = await connection.QueryAsync<FishingMethod>(new CommandDefinition(
                sql,
                cancellationToken: cancellationToken));
            return Result.Ok<IReadOnlyList<FishingMethod>>([.. rows]);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, MethodsFailedMessage);
            return Result.Fail<IReadOnlyList<FishingMethod>>(MethodsFailedMessage);
        }
    }

    public async Task<Result<IReadOnlyList<Species>>> GetAllSpeciesAsync(CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                SELECT "Id", "Code", "Name", "CreatedOn"
                FROM "Species"
                ORDER BY "Name";
                """;
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var rows = await connection.QueryAsync<Species>(new CommandDefinition(
                sql,
                cancellationToken: cancellationToken));
            return Result.Ok<IReadOnlyList<Species>>([.. rows]);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, SpeciesFailedMessage);
            return Result.Fail<IReadOnlyList<Species>>(SpeciesFailedMessage);
        }
    }
}
