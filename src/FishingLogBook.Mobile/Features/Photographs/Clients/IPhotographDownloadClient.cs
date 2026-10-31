using FishingLogBook.Mobile.Features.Photographs.Models;

namespace FishingLogBook.Mobile.Features.Photographs.Clients;

public interface IPhotographDownloadClient
{
    Task<PhotographDownloadResultModel> DownloadAsync(string downloadUrl, CancellationToken cancellationToken);
}
