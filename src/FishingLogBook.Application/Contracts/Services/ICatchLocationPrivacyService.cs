using FishingLogBook.Domain.Catches;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Application.Contracts.Services;

public interface ICatchLocationPrivacyService
{
    Task<CatchLocationExposureDto?> GetExposureAsync(
        Catch catchRecord,
        Guid viewerUserId,
        CancellationToken cancellationToken);
}
