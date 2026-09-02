using Dapper;
using FishingLogBook.Application.SystemStatus.Contracts.Repositories;
using FishingLogBook.Domain.SystemStatus;

namespace FishingLogBook.Infrastructure.Persistence.Repositories;

public sealed class SystemRepository : ISystemRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SystemRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<SystemHealth?> GetSystemHealthAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, name, createdon
            FROM systemhealth
            ORDER BY createdon
            LIMIT 1;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);

        return await connection.QueryFirstOrDefaultAsync<SystemHealth>(command);
    }
}
