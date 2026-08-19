using FishingLogBook.Application.Args;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Domain.Users;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Application.Users.Services;

public sealed class UserIdentityService : IUserIdentityService
{
    private const string MissingIdentityMessage = "External identity is missing.";
    private const string MissingEmailMessage = "Authenticated email is missing.";
    private const string EmptyUserIdMessage = "FishingLogBook UserId cannot be empty.";

    private readonly IUserIdentityRepository _userIdentityRepository;
    private readonly ILogger<UserIdentityService> _logger;

    public UserIdentityService(
        IUserIdentityRepository userIdentityRepository,
        ILogger<UserIdentityService> logger)
    {
        _userIdentityRepository = userIdentityRepository;
        _logger = logger;
    }

    public async Task<Result<Guid>> ResolveAsync(
        ResolveUserIdentityArgs args,
        CancellationToken cancellationToken)
    {
        if (args is null
            || string.IsNullOrWhiteSpace(args.Provider)
            || string.IsNullOrWhiteSpace(args.Subject))
        {
            return Result.Fail<Guid>(MissingIdentityMessage);
        }

        if (string.IsNullOrWhiteSpace(args.Email))
        {
            return Result.Fail<Guid>(MissingEmailMessage);
        }

        var existing = await _userIdentityRepository.FindUserIdAsync(
            new FindUserIdentityArgs
            {
                Provider = args.Provider,
                Subject = args.Subject
            },
            cancellationToken);
        if (existing.IsFailed)
        {
            return Result.Fail<Guid>(existing.Errors);
        }

        if (existing.Value is Guid existingUserId)
        {
            return await CompleteExistingAsync(existingUserId, args.Email, cancellationToken);
        }

        return await CompleteCreateAsync(args, cancellationToken);
    }

    private async Task<Result<Guid>> CompleteExistingAsync(
        Guid userId,
        string email,
        CancellationToken cancellationToken)
    {
        var ensured = EnsureUserId(userId, created: false);
        if (ensured.IsFailed)
        {
            return ensured;
        }

        var updated = await _userIdentityRepository.UpdateEmailAsync(
            new User { Id = userId, Email = email },
            cancellationToken);
        if (updated.IsFailed)
        {
            return Result.Fail<Guid>(updated.Errors);
        }

        return ensured;
    }

    private async Task<Result<Guid>> CompleteCreateAsync(
        ResolveUserIdentityArgs args,
        CancellationToken cancellationToken)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = args.Email
        };
        var identity = new UserIdentity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Provider = args.Provider,
            Subject = args.Subject
        };
        var created = await _userIdentityRepository.CreateAsync(user, identity, cancellationToken);
        if (created.IsFailed)
        {
            return created;
        }

        return EnsureUserId(created.Value, created: true);
    }

    private Result<Guid> EnsureUserId(Guid userId, bool created)
    {
        if (userId == Guid.Empty)
        {
            return Result.Fail<Guid>(EmptyUserIdMessage);
        }

        if (created)
        {
            _logger.LogInformation("Created FishingLogBook user {UserId}", userId);
        }
        else
        {
            _logger.LogDebug("Resolved FishingLogBook user {UserId}", userId);
        }

        return Result.Ok(userId);
    }
}
