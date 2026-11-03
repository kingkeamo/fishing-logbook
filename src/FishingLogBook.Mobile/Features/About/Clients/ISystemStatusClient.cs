using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.About.Clients;

public interface ISystemStatusClient
{
    Task<ApiResultModel<HealthDto>> GetApiHealthAsync(CancellationToken cancellationToken);

    Task<ApiResultModel<DatabaseTestDto>> GetDatabaseStatusAsync(CancellationToken cancellationToken);

    Task<ApiResultModel<BuildMetadataDto>> GetBuildMetadataAsync(CancellationToken cancellationToken);
}
