using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Catch.Clients;

public interface ICatchSyncClient
{
    Task<ApiResultModel<CatchDto>> UpsertAsync(CatchDto catchDto, CancellationToken cancellationToken);

    Task<ApiResultModel<PhotographUploadDto>> CreatePhotographUploadAsync(
        Guid catchId,
        PhotographUploadRequestDto request,
        CancellationToken cancellationToken);

    Task<ApiResultModel<NoContentModel>> RecordPhotographAsync(
        Guid catchId,
        RecordPhotographDto request,
        CancellationToken cancellationToken);

    Task<ApiResultModel<NoContentModel>> DeletePhotographAsync(
        Guid catchId,
        Guid photographId,
        CancellationToken cancellationToken);
}
