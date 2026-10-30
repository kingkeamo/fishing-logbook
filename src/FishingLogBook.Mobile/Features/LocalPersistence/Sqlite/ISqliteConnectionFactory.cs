using Microsoft.Data.Sqlite;

namespace FishingLogBook.Mobile.Features.LocalPersistence.Sqlite;

public interface ISqliteConnectionFactory
{
    Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken);
}
