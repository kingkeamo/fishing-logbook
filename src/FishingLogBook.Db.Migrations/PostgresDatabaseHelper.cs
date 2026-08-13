using System.Text.RegularExpressions;
using Npgsql;

namespace FishingLogBook.Db.Migrations;

public class PostgresDatabaseHelper : IPostgresDatabaseHelper
{
    public string GetAdminConnectionString(string baseConnectionString)
    {
        return GetConnectionString(baseConnectionString, "postgres");
    }

    public string GetConnectionString(string baseConnectionString, string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = databaseName
        };
        return builder.ToString();
    }

    public async Task<bool> CheckDatabaseExists(NpgsqlConnection connection, string databaseName)
    {
        var checkSql = "SELECT 1 FROM pg_database WHERE datname = @databaseName";
        await using var checkCmd = new NpgsqlCommand(checkSql, connection);
        checkCmd.Parameters.AddWithValue("databaseName", databaseName);
        var exists = await checkCmd.ExecuteScalarAsync();
        return exists != null;
    }

    public async Task TerminateConnections(NpgsqlConnection connection, string databaseName)
    {
        var terminateSql = @"
      SELECT pg_terminate_backend(pg_stat_activity.pid)
      FROM pg_stat_activity
      WHERE pg_stat_activity.datname = @databaseName
        AND pid <> pg_backend_pid()";

        await using var terminateCmd = new NpgsqlCommand(terminateSql, connection);
        terminateCmd.Parameters.AddWithValue("databaseName", databaseName);
        await terminateCmd.ExecuteNonQueryAsync();
    }

    public async Task DropDatabase(NpgsqlConnection connection, string databaseName)
    {
        ValidateDatabaseName(databaseName);
        var dropSql = $"DROP DATABASE IF EXISTS \"{databaseName}\"";
        await using var dropCmd = new NpgsqlCommand(dropSql, connection);
        await dropCmd.ExecuteNonQueryAsync();
    }

    public void ValidateDatabaseName(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new ArgumentException("Database name cannot be null or empty", nameof(databaseName));
        }

        if (databaseName.Contains('"') || databaseName.Contains(';') || databaseName.Contains('\\'))
        {
            throw new ArgumentException($"Database name contains invalid characters: {databaseName}", nameof(databaseName));
        }

        if (!Regex.IsMatch(databaseName, @"^[a-zA-Z0-9_\-]+$"))
        {
            throw new ArgumentException($"Database name contains invalid characters. Only alphanumeric, underscore, and hyphen allowed: {databaseName}", nameof(databaseName));
        }
    }

    public void ValidateIdentifier(string? identifier, string paramName)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Identifier cannot be null or empty", paramName);
        }

        if (identifier.Contains('"') || identifier.Contains(';') || identifier.Contains('\\'))
        {
            throw new ArgumentException($"Identifier contains invalid characters: {identifier}", paramName);
        }
    }
}
