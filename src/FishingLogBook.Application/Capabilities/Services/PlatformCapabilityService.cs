using FishingLogBook.Application.Args;
using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Users;
using FluentResults;

namespace FishingLogBook.Application.Capabilities.Services;

public sealed class PlatformCapabilityService : IPlatformCapabilityService
{
    private readonly IUserPlatformCapabilityRepository _userPlatformCapabilityRepository;
    private readonly ICurrentUser _currentUser;

    public PlatformCapabilityService(
        IUserPlatformCapabilityRepository userPlatformCapabilityRepository,
        ICurrentUser currentUser)
    {
        _userPlatformCapabilityRepository = userPlatformCapabilityRepository;
        _currentUser = currentUser;
    }

    public Task<Result<bool>> HasAsync(
        Guid userId,
        PlatformCapabilityEnum capability,
        CancellationToken cancellationToken)
    {
        return _userPlatformCapabilityRepository.HasAsync(
            new FindUserPlatformCapabilityArgs
            {
                UserId = userId,
                Capability = capability
            },
            cancellationToken);
    }

    public Task<Result<IReadOnlyList<PlatformCapabilityEnum>>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return _userPlatformCapabilityRepository.GetForUserAsync(
            new FindUserPlatformCapabilitiesArgs
            {
                UserId = userId
            },
            cancellationToken);
    }

    public async Task<Result> GrantAsync(GrantPlatformCapabilityArgs args, CancellationToken cancellationToken)
    {
        var authorised = await RequireAdministratorAsync(cancellationToken);
        if (authorised.IsFailed)
        {
            return authorised;
        }

        return await _userPlatformCapabilityRepository.GrantAsync(
            new UserPlatformCapability
            {
                UserId = args.TargetUserId,
                Capability = args.Capability
            },
            cancellationToken);
    }

    public async Task<Result> RevokeAsync(RevokePlatformCapabilityArgs args, CancellationToken cancellationToken)
    {
        var authorised = await RequireAdministratorAsync(cancellationToken);
        if (authorised.IsFailed)
        {
            return authorised;
        }

        return await _userPlatformCapabilityRepository.RevokeAsync(
            new FindUserPlatformCapabilityArgs
            {
                UserId = args.TargetUserId,
                Capability = args.Capability
            },
            cancellationToken);
    }

    private async Task<Result> RequireAdministratorAsync(CancellationToken cancellationToken)
    {
        if (!_currentUser.IsResolved)
        {
            return Result.Fail(new CurrentUserUnresolvedError());
        }

        var hasAdministrator = await HasAsync(
            _currentUser.UserId,
            PlatformCapabilityEnum.Administrator,
            cancellationToken);
        if (hasAdministrator.IsFailed)
        {
            return Result.Fail(hasAdministrator.Errors);
        }

        if (!hasAdministrator.Value)
        {
            return Result.Fail(new MissingPlatformCapabilityError());
        }

        return Result.Ok();
    }
}
