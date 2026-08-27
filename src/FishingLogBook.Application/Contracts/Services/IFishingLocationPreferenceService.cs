using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.Contracts.Services;

public interface IFishingLocationPreferenceService
{
    Task<Result<FishingLocationPreferencesDto>> GetPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<Result<FishingLocationPreferencesDto>> UpdatePreferencesAsync(
        Guid userId,
        UpdateFishingLocationPreferencesDto dto,
        CancellationToken cancellationToken);
}
