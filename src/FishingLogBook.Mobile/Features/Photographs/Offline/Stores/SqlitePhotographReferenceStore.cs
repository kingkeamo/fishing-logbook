using FishingLogBook.Mobile.Common.Offline.Sqlite;
using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Features.Photographs.Offline.Stores;

public sealed class SqlitePhotographReferenceStore(
    ISqliteConnectionFactory connectionFactory,
    ISqliteDatabase database) : IPhotographReferenceStore
{
    public async Task<IReadOnlySet<string>> GetReferencedRelativePathsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await database.EnsureInitializedAsync(cancellationToken);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT DISTINCT localfilepath
            FROM localcatchphotographs
            WHERE userid = $userid
              AND localfilepath IS NOT NULL
              AND localfilepath <> ''
            """;
        command.Parameters.AddWithValue("$userid", userId.ToString());
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var path = reader.GetString(0).Replace('\\', '/');
            paths.Add(path);
        }

        return paths;
    }
}
