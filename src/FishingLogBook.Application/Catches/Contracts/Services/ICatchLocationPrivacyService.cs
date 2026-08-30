using FishingLogBook.Domain.Catches;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Application.Catches.Contracts.Services;

public interface ICatchLocationPrivacyService
{
    Task<CatchLocationExposureDto?> GetExposureAsync(
        Catch catchRecord,
        Guid viewerUserId,
        CancellationToken cancellationToken);
}
