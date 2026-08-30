using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Features.Catch.Clients;

public interface ICatchClient
{
    Task<CatchDto?> UpsertAsync(CatchDto catchRecord, CancellationToken cancellationToken);

    Task<IReadOnlyList<CatchViewDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<CatchViewDto?> GetAsync(Guid catchId, CancellationToken cancellationToken);

    Task<byte[]> DownloadPhotographAsync(string url, CancellationToken cancellationToken);

    Task<PhotographUploadDto> CreatePhotographUploadAsync(
        Guid catchId,
        PhotographUploadRequestDto request,
        CancellationToken cancellationToken);

    Task UploadPhotographAsync(
        string uploadUrl,
        byte[] bytes,
        string contentType,
        CancellationToken cancellationToken);

    Task RecordPhotographAsync(
        Guid catchId,
        RecordPhotographDto request,
        CancellationToken cancellationToken);

    Task DeletePhotographAsync(Guid catchId, Guid photographId, CancellationToken cancellationToken);

    Task UpdateLocationVisibilityAsync(Guid catchId, string visibility, CancellationToken cancellationToken);

    Task<CatchAnglerCorrectionResult> CorrectAnglerAsync(Guid catchId, Guid anglerUserId, CancellationToken cancellationToken);
}
