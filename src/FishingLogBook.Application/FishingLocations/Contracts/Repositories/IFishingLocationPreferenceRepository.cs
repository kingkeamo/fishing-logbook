using FishingLogBook.Domain.FishingLocations;
using FluentResults;

namespace FishingLogBook.Application.FishingLocations.Contracts.Repositories;

public interface IFishingLocationPreferenceRepository
{
    Task<Result<IReadOnlyList<UserFishingLocationPreference>>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<Result> ReplaceAsync(
        Guid userId,
        IReadOnlyList<UserFishingLocationPreference> locations,
        CancellationToken cancellationToken);
}
