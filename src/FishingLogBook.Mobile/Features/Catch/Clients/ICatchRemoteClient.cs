using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Catch.Clients;

public interface ICatchRemoteClient
{
    Task<ApiResultModel<List<CatchViewDto>>> ListAsync(CancellationToken cancellationToken);

    Task<ApiResultModel<CatchViewDto>> GetAsync(Guid catchId, CancellationToken cancellationToken);
}
