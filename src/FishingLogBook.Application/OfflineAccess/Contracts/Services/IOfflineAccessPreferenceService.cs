using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.OfflineAccess.Contracts.Services;

public interface IOfflineAccessPreferenceService
{
    Task<Result<OfflineAccessPreferenceDto>> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<OfflineAccessPreferenceDto>> SetAsync(
        Guid userId,
        bool enabled,
        CancellationToken cancellationToken);
}
