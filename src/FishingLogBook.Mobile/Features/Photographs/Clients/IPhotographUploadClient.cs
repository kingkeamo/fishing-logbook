using FishingLogBook.Mobile.Features.Photographs.Enums;

namespace FishingLogBook.Mobile.Features.Photographs.Clients;

public interface IPhotographUploadClient
{
    Task<PhotographUploadOutcomeEnum> UploadAsync(
        string uploadUrl,
        Stream content,
        string contentType,
        CancellationToken cancellationToken);
}
