using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Services;

public interface ISystemStatusClient
{
    Task<HealthDto?> GetApiHealthAsync(CancellationToken cancellationToken);

    Task<DatabaseTestDto?> GetDatabaseStatusAsync(CancellationToken cancellationToken);
}
