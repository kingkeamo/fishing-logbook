using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Features.SystemStatus.Clients;

public interface ISystemStatusClient
{
    Task<HealthDto?> GetApiHealthAsync(CancellationToken cancellationToken);

    Task<bool> IsApiReachableAsync(CancellationToken cancellationToken);

    Task<DatabaseTestDto?> GetDatabaseStatusAsync(CancellationToken cancellationToken);

    Task<BuildMetadataDto?> GetBuildMetadataAsync(CancellationToken cancellationToken);
}
