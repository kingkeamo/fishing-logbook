using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Features.Catch.Clients;

public interface ICatchClient
{
    Task UpsertAsync(CatchDto catchRecord, CancellationToken cancellationToken);

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
}
