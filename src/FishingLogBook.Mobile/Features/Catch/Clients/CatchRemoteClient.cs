using FishingLogBook.Mobile.Api.Clients;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Catch.Clients;

public sealed class CatchRemoteClient(AuthorizedApiClient apiClient) : ICatchRemoteClient
{
    private const string CatchesPath = "api/catches";

    public Task<ApiResultModel<List<CatchViewDto>>> ListAsync(CancellationToken cancellationToken)
    {
        return apiClient.GetAsync<List<CatchViewDto>>(CatchesPath, cancellationToken);
    }

    public Task<ApiResultModel<CatchViewDto>> GetAsync(Guid catchId, CancellationToken cancellationToken)
    {
        return apiClient.GetAsync<CatchViewDto>(
            $"{CatchesPath}/{catchId:D}",
            cancellationToken,
            distinguishNotFound: true);
    }
}
