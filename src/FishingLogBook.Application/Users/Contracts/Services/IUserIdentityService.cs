using FishingLogBook.Application.Args;
using FluentResults;

namespace FishingLogBook.Application.Users.Contracts.Services;

public interface IUserIdentityService
{
    Task<Result<Guid>> ResolveAsync(ResolveUserIdentityArgs args, CancellationToken cancellationToken);
}
