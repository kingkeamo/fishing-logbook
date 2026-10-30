using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Common.Offline.Sqlite;

public sealed class SqliteMigrator : ISqliteMigrator
{
    private static readonly Regex MigrationNameRegex = new(
        @"\.Migrations\.(\d+)_([^.]+)\.sql$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly ILogger<SqliteMigrator> _logger;
    private readonly Assembly _resourceAssembly;

    public SqliteMigrator(
        ISqliteConnectionFactory connectionFactory,
        ILogger<SqliteMigrator> logger)
        : this(connectionFactory, logger, typeof(SqliteMigrator).Assembly)
    {
    }

    public SqliteMigrator(
        ISqliteConnectionFactory connectionFactory,
        ILogger<SqliteMigrator> logger,
        Assembly resourceAssembly)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(resourceAssembly);
        _connectionFactory = connectionFactory;
        _logger = logger;
        _resourceAssembly = resourceAssembly;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        var migrations = LoadMigrations();
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        var currentVersion = await ReadVersionAsync(connection, cancellationToken);
        foreach (var migration in migrations)
        {
            if (migration.Version <= currentVersion)
            {
                continue;
            }

            await ApplyMigrationAsync(connection, migration, cancellationToken);
            currentVersion = migration.Version;
        }
    }

    private async Task ApplyMigrationAsync(
        SqliteConnection connection,
        MigrationScript migration,
        CancellationToken cancellationToken)
    {
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = migration.Sql;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var versionCommand = connection.CreateCommand())
            {
                versionCommand.Transaction = transaction;
                versionCommand.CommandText =
                    """
                    UPDATE schemainfo SET version = $version;
                    """;
                versionCommand.Parameters.AddWithValue("$version", migration.Version);
                var updated = await versionCommand.ExecuteNonQueryAsync(cancellationToken);
                if (updated != 1)
                {
                    throw new InvalidOperationException(
                        $"Migration {migration.Version} did not update schemainfo.");
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Local SQLite migration {MigrationVersion} failed; rolling back.",
                migration.Version);
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception rollbackException)
            {
                _logger.LogError(
                    rollbackException,
                    "Rollback failed after migration {MigrationVersion} error.",
                    migration.Version);
            }

            throw;
        }
    }

    private static async Task<int> ReadVersionAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText =
            """
            SELECT 1
            FROM sqlite_master
            WHERE type = 'table' AND name = 'schemainfo'
            LIMIT 1;
            """;
        var exists = await existsCommand.ExecuteScalarAsync(cancellationToken);
        if (exists is null)
        {
            return 0;
        }

        await using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "SELECT version FROM schemainfo LIMIT 1;";
        var value = await versionCommand.ExecuteScalarAsync(cancellationToken);
        return value is long version
            ? (int)version
            : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private IReadOnlyList<MigrationScript> LoadMigrations()
    {
        var migrations = new List<MigrationScript>();
        foreach (var resourceName in _resourceAssembly.GetManifestResourceNames())
        {
            var match = MigrationNameRegex.Match(resourceName);
            if (!match.Success)
            {
                continue;
            }

            var version = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            using var stream = _resourceAssembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Missing embedded migration '{resourceName}'.");
            using var reader = new StreamReader(stream);
            var sql = reader.ReadToEnd();
            if (string.IsNullOrWhiteSpace(sql))
            {
                throw new InvalidOperationException($"Migration '{resourceName}' is empty.");
            }

            migrations.Add(new MigrationScript(version, sql));
        }

        if (migrations.Count == 0)
        {
            throw new InvalidOperationException("No embedded SQLite migrations were found.");
        }

        return migrations.OrderBy(migration => migration.Version).ToArray();
    }

    private sealed record MigrationScript(int Version, string Sql);
}
