using Microsoft.Extensions.Logging;
using Npgsql;

namespace FishingLogBook.Db.Migrations;

public class EphemeralDatabaseService
{
    private readonly ILogger<EphemeralDatabaseService> _logger;
    private readonly MigrationService _migrationService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IPostgresDatabaseHelper _dbHelper;

    public EphemeralDatabaseService(ILogger<EphemeralDatabaseService> logger, ILoggerFactory loggerFactory, IPostgresDatabaseHelper dbHelper)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _dbHelper = dbHelper;
        var migrationLogger = _loggerFactory.CreateLogger<MigrationService>();
        _migrationService = new MigrationService(migrationLogger);
    }

    public async Task<bool> EnsureEphemeralDatabaseExists(string baseConnectionString, string envName)
    {
        var (sourceDatabaseName, targetDatabaseName) = GetDatabaseNames(baseConnectionString, envName);

        _logger.LogInformation("Checking if ephemeral database {TargetDatabase} exists", targetDatabaseName);

        if (DatabaseAlreadyExists(baseConnectionString, targetDatabaseName))
        {
            _logger.LogInformation("Ephemeral database {TargetDatabase} already exists", targetDatabaseName);
            return true;
        }

        _logger.LogInformation("Ephemeral database {TargetDatabase} does not exist. Creating from {SourceDatabase}", targetDatabaseName, sourceDatabaseName);

        if (!await TryCreateDatabaseCopy(baseConnectionString, sourceDatabaseName, targetDatabaseName))
        {
            _logger.LogError("Failed to create database copy");
            return false;
        }

        return TryRunMigrationsOnNewDatabase(baseConnectionString, targetDatabaseName);
    }

    private (string sourceDatabaseName, string targetDatabaseName) GetDatabaseNames(string baseConnectionString, string envName)
    {
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString);
        var sourceDatabaseName = builder.Database ?? throw new InvalidOperationException("Source database name not found in connection string");
        var targetDatabaseName = $"fishinglogbook-{envName}";
        return (sourceDatabaseName, targetDatabaseName);
    }

    private bool DatabaseAlreadyExists(string baseConnectionString, string databaseName)
    {
        var connectionString = _dbHelper.GetConnectionString(baseConnectionString, databaseName);
        return _migrationService.DatabaseExists(connectionString);
    }

    private bool TryRunMigrationsOnNewDatabase(string baseConnectionString, string databaseName)
    {
        var connectionString = _dbHelper.GetConnectionString(baseConnectionString, databaseName);
        var migrationsAssembly = typeof(MigrationService).Assembly;
        var upgradeEngine = _migrationService.CreateUpgradeEngine(connectionString, migrationsAssembly);
        return _migrationService.RunMigrations(upgradeEngine);
    }

    private async Task<bool> TryCreateDatabaseCopy(string baseConnectionString, string sourceDatabaseName, string targetDatabaseName)
    {
        try
        {
            _logger.LogInformation("Creating database {TargetDatabase} as a copy of {SourceDatabase}", targetDatabaseName, sourceDatabaseName);

            var adminConnectionString = _dbHelper.GetAdminConnectionString(baseConnectionString);

            using var connection = new NpgsqlConnection(adminConnectionString);
            await connection.OpenAsync();

            await _dbHelper.TerminateConnections(connection, sourceDatabaseName);
            await CreateDatabaseFromTemplate(connection, baseConnectionString, sourceDatabaseName, targetDatabaseName);

            _logger.LogInformation("Successfully created database {TargetDatabase}", targetDatabaseName);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create database copy from {SourceDatabase} to {TargetDatabase}", sourceDatabaseName, targetDatabaseName);
            return false;
        }
    }

    private async Task CreateDatabaseFromTemplate(NpgsqlConnection connection, string baseConnectionString, string sourceDatabaseName, string targetDatabaseName)
    {
        _dbHelper.ValidateDatabaseName(sourceDatabaseName);
        _dbHelper.ValidateDatabaseName(targetDatabaseName);

        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString);
        var username = builder.Username;
        _dbHelper.ValidateIdentifier(username, nameof(username));

        using var createCmd = new NpgsqlCommand();
        createCmd.Connection = connection;
        createCmd.CommandText = $@"
        CREATE DATABASE ""{targetDatabaseName}""
        WITH TEMPLATE ""{sourceDatabaseName}""
        OWNER ""{username}""";
        await createCmd.ExecuteNonQueryAsync();
    }
}
