using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Features.OfflineAccess.Clients;

public interface IOfflineAccessPreferenceClient
{
    Task<OfflineAccessPreferenceDto> GetAsync(CancellationToken cancellationToken);
    Task<OfflineAccessPreferenceDto> SetAsync(bool enabled, CancellationToken cancellationToken);
}
