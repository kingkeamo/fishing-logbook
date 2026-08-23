using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.OfflineAccess.Services;

public sealed class OfflineAccessPreferenceService : IOfflineAccessPreferenceService
{
    private readonly IOfflineAccessPreferenceRepository _repository;

    public OfflineAccessPreferenceService(IOfflineAccessPreferenceRepository repository)
    {
        _repository = repository;
    }

    public Task<Result<OfflineAccessPreferenceDto>> GetAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        _repository.GetAsync(userId, cancellationToken);

    public Task<Result<OfflineAccessPreferenceDto>> SetAsync(
        Guid userId,
        bool enabled,
        CancellationToken cancellationToken) =>
        _repository.SetAsync(userId, enabled, cancellationToken);
}
