using FishingLogBook.Shared.SystemStatus;

namespace FishingLogBook.Web.Services;

public interface ISystemStatusClient
{
    Task<HealthResponse?> GetApiHealthAsync(CancellationToken cancellationToken);

    Task<DatabaseTestResponse?> GetDatabaseStatusAsync(CancellationToken cancellationToken);
}
