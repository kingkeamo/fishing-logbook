using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Enums;
using FluentResults;

namespace FishingLogBook.Application.Contracts.Services;

public interface IPlatformCapabilityService
{
    Task<Result<bool>> HasAsync(
        Guid userId,
        PlatformCapabilityEnum capability,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<PlatformCapabilityEnum>>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<Result> GrantAsync(GrantPlatformCapabilityArgs args, CancellationToken cancellationToken);

    Task<Result> RevokeAsync(RevokePlatformCapabilityArgs args, CancellationToken cancellationToken);
}
