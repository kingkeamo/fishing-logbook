using System.Reflection;
using DbUp;
using FishingLogBook.Application.Contracts;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Infrastructure.Migrations;

public sealed class DbUpDatabaseMigrator : IDatabaseMigrator
{
    private const string MigrationResourcePrefix = "FishingLogBook.Infrastructure.Migrations.";

    private readonly string _connectionString;
    private readonly ILogger<DbUpDatabaseMigrator> _logger;

    public DbUpDatabaseMigrator(string connectionString, ILogger<DbUpDatabaseMigrator> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public DatabaseMigrationResult Migrate()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _logger.LogWarning("Database migrations skipped: no PostgreSQL connection string is configured.");
            return new DatabaseMigrationResult(false, Array.Empty<string>(), "No connection string configured.");
        }

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(_connectionString)
            .WithScriptsEmbeddedInAssembly(
                Assembly.GetExecutingAssembly(),
                name => name.StartsWith(MigrationResourcePrefix, StringComparison.Ordinal))
            .WithTransactionPerScript()
            .LogTo(new LoggerUpgradeLog(_logger))
            .Build();

        _logger.LogInformation("Starting database migration.");
        var result = upgrader.PerformUpgrade();
        var scripts = result.Scripts.Select(script => script.Name).ToList();

        if (!result.Successful)
        {
            _logger.LogError(result.Error, "Database migration failed.");
            return new DatabaseMigrationResult(false, scripts, result.Error?.Message);
        }

        _logger.LogInformation("Database migration completed successfully. {ScriptCount} script(s) executed.", scripts.Count);
        return new DatabaseMigrationResult(true, scripts, null);
    }
}
