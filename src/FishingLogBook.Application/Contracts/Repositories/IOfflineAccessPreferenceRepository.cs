using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.Contracts.Repositories;

public interface IOfflineAccessPreferenceRepository
{
    Task<Result<OfflineAccessPreferenceDto>> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<OfflineAccessPreferenceDto>> SetAsync(
        Guid userId,
        bool enabled,
        CancellationToken cancellationToken);
}
