using System.Reflection;
using DbUp;
using DbUp.Engine;
using DbUp.Engine.Output;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FishingLogBook.Db.Migrations;

public class MigrationService
{
    private const string MigrationResourcePrefix = "FishingLogBook.Db.Migrations";

    private readonly ILogger<MigrationService> _logger;

    public MigrationService(ILogger<MigrationService> logger)
    {
        _logger = logger;
    }

    public bool DatabaseExists(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database;

        builder.Database = "postgres";
        var adminConnectionString = builder.ToString();

        using var connection = new NpgsqlConnection(adminConnectionString);
        connection.Open();

        using var command = new NpgsqlCommand();
        command.Connection = connection;
        command.CommandText = "SELECT COUNT(*) FROM pg_database WHERE datname = @databaseName";
        command.Parameters.AddWithValue("databaseName", databaseName!);

        var result = command.ExecuteScalar();
        var count = Convert.ToInt32(result);

        if (count <= 0)
        {
            _logger.LogError("Database {DatabaseName} does not exist", databaseName);
            return false;
        }

        return true;
    }

    public UpgradeEngine CreateUpgradeEngine(string connectionString, Assembly migrationAssembly)
    {
        return DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                migrationAssembly,
                s => s.StartsWith(MigrationResourcePrefix, StringComparison.Ordinal))
            .WithScriptNameComparer(new FilenameOnlyScriptComparer())
            .WithTransactionPerScript()
            .LogTo(new DbUpLogger(_logger))
            .Build();
    }

    public bool RunMigrations(UpgradeEngine upgradeEngine)
    {
        try
        {
            _logger.LogInformation("Running migrations");

            if (!upgradeEngine.IsUpgradeRequired())
            {
                _logger.LogInformation("No new migrations found. Database is up to date");
                return true;
            }

            var result = upgradeEngine.PerformUpgrade();
            if (!result.Successful)
            {
                _logger.LogError(result.Error, "Failed to run migrations");
                return false;
            }

            _logger.LogInformation("Successfully applied {Count} migrations", result.Scripts.Count());
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running migrations");
            return false;
        }
    }

    private sealed class DbUpLogger : IUpgradeLog
    {
        private readonly ILogger _logger;

        public DbUpLogger(ILogger logger)
        {
            _logger = logger;
        }

        public void LogTrace(string format, params object[] args)
        {
            _logger.LogTrace(format, args);
        }

        public void LogDebug(string format, params object[] args)
        {
            _logger.LogDebug(format, args);
        }

        public void LogInformation(string format, params object[] args)
        {
            _logger.LogInformation(format, args);
        }

        public void LogWarning(string format, params object[] args)
        {
            _logger.LogWarning(format, args);
        }

        public void LogError(string format, params object[] args)
        {
            _logger.LogError(format, args);
        }

        public void LogError(Exception exception, string format, params object[] args)
        {
            _logger.LogError(exception, format, args);
        }
    }
}
