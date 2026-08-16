using FishingLogBook.Db.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace FishingLogBook.Infrastructure.Tests.TestSupport;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        var migrations = new MigrationService(NullLogger<MigrationService>.Instance);
        var engine = migrations.CreateUpgradeEngine(ConnectionString, typeof(MigrationService).Assembly);
        if (!migrations.RunMigrations(engine))
        {
            throw new InvalidOperationException("Failed to apply migrations to the test database.");
        }
    }

    public Task DisposeAsync()
    {
        return _container.DisposeAsync().AsTask();
    }
}
