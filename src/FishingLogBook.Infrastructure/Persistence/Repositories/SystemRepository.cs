using Dapper;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Domain.SystemStatus;

namespace FishingLogBook.Infrastructure.Persistence.Repositories;

public sealed class SystemRepository : ISystemRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SystemRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<SystemTestRecord?> GetSystemTestRecordAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT "Id", "Name", "CreatedOn"
            FROM "SystemTest"
            ORDER BY "CreatedOn"
            LIMIT 1;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);

        return await connection.QueryFirstOrDefaultAsync<SystemTestRecord>(command);
    }
}
