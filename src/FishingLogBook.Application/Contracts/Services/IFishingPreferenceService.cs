using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.Contracts.Services;

public interface IFishingPreferenceService
{
    Task<Result<IReadOnlyList<FishingMethodDto>>> GetCatalogueMethodsAsync(CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<SpeciesDto>>> GetCatalogueSpeciesAsync(CancellationToken cancellationToken);

    Task<Result<FishingPreferencesDto>> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<FishingPreferencesDto>> UpdatePreferencesAsync(
        Guid userId,
        UpdateFishingPreferencesDto dto,
        CancellationToken cancellationToken);
}
