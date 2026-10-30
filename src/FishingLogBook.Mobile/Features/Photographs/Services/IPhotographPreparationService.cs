using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Models;

namespace FishingLogBook.Mobile.Features.Photographs.Services;

public interface IPhotographPreparationService
{
    long MaxPhotographBytes { get; }

    Task<PhotographPreparationResultModel> PrepareFromFileAsync(
        Guid userId,
        string sourceFilePath,
        string contentType,
        DateTimeOffset? fileLastModified,
        PhotographSourceEnum source,
        CancellationToken cancellationToken);
}
