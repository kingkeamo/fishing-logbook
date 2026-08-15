using Dapper;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Domain.TestCatches;

namespace FishingLogBook.Infrastructure.Persistence;

public sealed class TestCatchRepository : ITestCatchRepository
{
    private const string SelectSql = """
        SELECT t."Id", t."SpeciesName", t."CaughtOn", t."Notes",
               p."PhotographId", p."ObjectKey" AS "PhotographObjectKey", p."ContentType" AS "PhotographContentType"
        FROM "TestCatch" t
        LEFT JOIN "TestCatchPhotograph" p ON p."TestCatchId" = t."Id"
        """;

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

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(insertSql, record, cancellationToken: cancellationToken));
        return await QueryByIdAsync(connection, record.Id, cancellationToken)
            ?? throw new InvalidOperationException($"TestCatch {record.Id} was not found after upsert.");
    }

    public async Task<TestCatchRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await QueryByIdAsync(connection, id, cancellationToken);
    }

    public async Task<IReadOnlyList<TestCatchRecord>> GetAllAsync(CancellationToken cancellationToken)
    {
        const string sql = $"""
            {SelectSql}
            ORDER BY t."CaughtOn" DESC;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        var records = await connection.QueryAsync<TestCatchRecord>(command);
        return records.ToArray();
    }

    public async Task UpsertPhotographAsync(
        Guid testCatchId,
        Guid photographId,
        string objectKey,
        string contentType,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO "TestCatchPhotograph" ("TestCatchId", "PhotographId", "ObjectKey", "ContentType")
            VALUES (@TestCatchId, @PhotographId, @ObjectKey, @ContentType)
            ON CONFLICT ("TestCatchId") DO UPDATE
            SET "PhotographId" = EXCLUDED."PhotographId",
                "ObjectKey" = EXCLUDED."ObjectKey",
                "ContentType" = EXCLUDED."ContentType";
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { TestCatchId = testCatchId, PhotographId = photographId, ObjectKey = objectKey, ContentType = contentType },
            cancellationToken: cancellationToken));
    }

    private static Task<TestCatchRecord?> QueryByIdAsync(
        Npgsql.NpgsqlConnection connection,
        Guid id,
        CancellationToken cancellationToken)
    {
        const string sql = $"""
            {SelectSql}
            WHERE t."Id" = @Id;
            """;

        return connection.QuerySingleOrDefaultAsync<TestCatchRecord>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }
}
