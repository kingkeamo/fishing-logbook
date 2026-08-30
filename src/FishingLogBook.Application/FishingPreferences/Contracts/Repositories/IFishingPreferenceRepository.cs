using FishingLogBook.Domain.Catalogue;
using FluentResults;

namespace FishingLogBook.Application.FishingPreferences.Contracts.Repositories;

public interface IFishingPreferenceRepository
{
    Task<Result<IReadOnlyList<UserFishingMethodPreference>>> GetMethodPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<UserFishingSpeciesPreference>>> GetSpeciesPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<Result> ReplacePreferencesAsync(
        Guid userId,
        IReadOnlyList<UserFishingMethodPreference> methods,
        IReadOnlyList<UserFishingSpeciesPreference> species,
        CancellationToken cancellationToken);
}
