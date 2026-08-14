using Dapper;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Domain.TestCatches;

namespace FishingLogBook.Infrastructure.Persistence;

public sealed class TestCatchRepository : ITestCatchRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public TestCatchRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<TestCatchRecord> UpsertAsync(TestCatchRecord record, CancellationToken cancellationToken)
    {
        const string insertSql = """
            INSERT INTO "TestCatch" ("Id", "SpeciesName", "CaughtOn", "Notes")
            VALUES (@Id, @SpeciesName, @CaughtOn, @Notes)
            ON CONFLICT ("Id") DO NOTHING;
            """;

        const string selectSql = """
            SELECT "Id", "SpeciesName", "CaughtOn", "Notes"
            FROM "TestCatch"
            WHERE "Id" = @Id;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(insertSql, record, cancellationToken: cancellationToken));
        return await connection.QuerySingleAsync<TestCatchRecord>(
            new CommandDefinition(selectSql, new { record.Id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<TestCatchRecord>> GetAllAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT "Id", "SpeciesName", "CaughtOn", "Notes"
            FROM "TestCatch"
            ORDER BY "CaughtOn" DESC;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        var records = await connection.QueryAsync<TestCatchRecord>(command);
        return records.ToArray();
    }
}
