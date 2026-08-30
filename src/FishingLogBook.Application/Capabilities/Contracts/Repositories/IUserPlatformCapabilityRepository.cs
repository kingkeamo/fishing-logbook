using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Users;
using FluentResults;

namespace FishingLogBook.Application.Capabilities.Contracts.Repositories;

public interface IUserPlatformCapabilityRepository
{
    Task<Result<bool>> HasAsync(FindUserPlatformCapabilityArgs args, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<PlatformCapabilityEnum>>> GetForUserAsync(
        FindUserPlatformCapabilitiesArgs args,
        CancellationToken cancellationToken);

    Task<Result> GrantAsync(UserPlatformCapability association, CancellationToken cancellationToken);

    Task<Result> RevokeAsync(FindUserPlatformCapabilityArgs args, CancellationToken cancellationToken);
}
