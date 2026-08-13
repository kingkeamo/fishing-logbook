using Npgsql;

namespace FishingLogBook.Db.Migrations;

public interface IPostgresDatabaseHelper
{
    string GetAdminConnectionString(string baseConnectionString);

    string GetConnectionString(string baseConnectionString, string databaseName);

    Task<bool> CheckDatabaseExists(NpgsqlConnection connection, string databaseName);

    Task TerminateConnections(NpgsqlConnection connection, string databaseName);

    Task DropDatabase(NpgsqlConnection connection, string databaseName);

    void ValidateDatabaseName(string databaseName);

    void ValidateIdentifier(string? identifier, string paramName);
}
