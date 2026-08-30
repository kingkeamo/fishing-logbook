using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Users;
using FluentResults;

namespace FishingLogBook.Application.Users.Contracts.Repositories;

public interface IUserIdentityRepository
{
    Task<Result<Guid?>> FindUserIdAsync(FindUserIdentityArgs args, CancellationToken cancellationToken);

    Task<Result<Guid>> CreateAsync(User user, UserIdentity identity, CancellationToken cancellationToken);

    Task<Result> UpdateEmailAsync(User user, CancellationToken cancellationToken);
}
