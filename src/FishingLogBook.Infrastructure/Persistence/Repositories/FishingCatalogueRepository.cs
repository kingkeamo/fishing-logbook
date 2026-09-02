using Dapper;
using FishingLogBook.Application.FishingPreferences.Contracts.Repositories;
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
                SELECT id, code, name, createdon
                FROM fishingmethods
                ORDER BY name;
                """;
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var rows = await connection.QueryAsync<FishingMethod>(new CommandDefinition(
                sql,
                cancellationToken: cancellationToken));
            return Result.Ok<IReadOnlyList<FishingMethod>>([.. rows]);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(exception, "Fishing method catalogue read was cancelled.");
            throw;
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
                SELECT id, code, name, createdon
                FROM species
                ORDER BY name;
                """;
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var rows = await connection.QueryAsync<Species>(new CommandDefinition(
                sql,
                cancellationToken: cancellationToken));
            return Result.Ok<IReadOnlyList<Species>>([.. rows]);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(exception, "Species catalogue read was cancelled.");
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, SpeciesFailedMessage);
            return Result.Fail<IReadOnlyList<Species>>(SpeciesFailedMessage);
        }
    }
}
