using FishingLogBook.Domain.Users;
using FluentResults;

namespace FishingLogBook.Application.Contracts.Repositories;

public interface IUserIdentityRepository
{
    Task<Result<Guid?>> FindUserIdAsync(UserIdentity identity, CancellationToken cancellationToken);

    Task<Result<Guid>> CreateAsync(User user, UserIdentity identity, CancellationToken cancellationToken);

    Task<Result> UpdateEmailAsync(User user, CancellationToken cancellationToken);
}
