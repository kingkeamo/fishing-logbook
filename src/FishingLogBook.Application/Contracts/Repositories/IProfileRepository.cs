using FishingLogBook.Domain.Profiles;
using FluentResults;

namespace FishingLogBook.Application.Contracts.Repositories;

public interface IProfileRepository
{
    Task<Result<bool>> UserExistsAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<Profile?>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<Profile>> UpsertAsync(Profile profile, CancellationToken cancellationToken);

    Task<Result<Profile>> UpdatePhotographAsync(
        Guid userId,
        Guid photographId,
        string objectKey,
        string contentType,
        CancellationToken cancellationToken);
}
