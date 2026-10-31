using FishingLogBook.Mobile.Api.Clients;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Catch.Clients;

public sealed class CatchSyncClient(AuthorizedApiClient apiClient) : ICatchSyncClient
{
    private const string CatchesPath = "api/catches";

    public Task<ApiResultModel<CatchDto>> UpsertAsync(CatchDto catchDto, CancellationToken cancellationToken)
    {
        return apiClient.PostAsync<CatchDto, CatchDto>(CatchesPath, catchDto, cancellationToken);
    }

    public Task<ApiResultModel<PhotographUploadDto>> CreatePhotographUploadAsync(
        Guid catchId,
        PhotographUploadRequestDto request,
        CancellationToken cancellationToken)
    {
        return apiClient.PostAsync<PhotographUploadRequestDto, PhotographUploadDto>(
            $"{CatchesPath}/{catchId:D}/photographs/upload-url",
            request,
            cancellationToken);
    }

    public Task<ApiResultModel<NoContentModel>> RecordPhotographAsync(
        Guid catchId,
        RecordPhotographDto request,
        CancellationToken cancellationToken)
    {
        return apiClient.PostNoContentAsync(
            $"{CatchesPath}/{catchId:D}/photographs",
            request,
            cancellationToken);
    }

    public Task<ApiResultModel<NoContentModel>> DeletePhotographAsync(
        Guid catchId,
        Guid photographId,
        CancellationToken cancellationToken)
    {
        return apiClient.DeleteAsync(
            $"{CatchesPath}/{catchId:D}/photographs/{photographId:D}",
            cancellationToken);
    }
}
