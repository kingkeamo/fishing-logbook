using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Features.SystemStatus.Services;

public interface ISystemStatusClient
{
    Task<HealthDto?> GetApiHealthAsync(CancellationToken cancellationToken);

    Task<DatabaseTestDto?> GetDatabaseStatusAsync(CancellationToken cancellationToken);
}
