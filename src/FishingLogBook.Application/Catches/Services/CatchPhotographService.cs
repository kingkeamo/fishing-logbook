using FishingLogBook.Application.Args;
using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Application.Catches.Services;

public sealed class CatchPhotographService : ICatchPhotographService
{
    private static readonly TimeSpan UploadLifetime = TimeSpan.FromMinutes(15);

    private readonly ICatchRepository _catchRepository;
    private readonly IObjectStorage _objectStorage;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<CatchPhotographService> _logger;

    public CatchPhotographService(
        ICatchRepository catchRepository,
        IObjectStorage objectStorage,
        ICurrentUser currentUser,
        ILogger<CatchPhotographService> logger)
    {
        _catchRepository = catchRepository;
        _objectStorage = objectStorage;
        _currentUser = currentUser;
        _logger = logger;
    }

    public bool IsObjectStorageConfigured
    {
        get
        {
            return _objectStorage.IsConfigured;
        }
    }

    public async Task<Result<PhotographUploadDto>> CreateUploadAsync(
        CreateCatchPhotographUploadArgs args,
        CancellationToken cancellationToken)
    {
        var owner = await ResolveCatchOwnerAsync(args.CatchId, cancellationToken);
        if (owner.IsFailed)
        {
            return Result.Fail<PhotographUploadDto>(owner.Errors);
        }

        var photograph = await LoadOwnedPhotographAsync(
            owner.Value,
            args.CatchId,
            args.Request.PhotographId,
            cancellationToken);
        if (photograph.IsFailed)
        {
            return Result.Fail<PhotographUploadDto>(photograph.Errors);
        }

        if (!string.Equals(
                photograph.Value.ContentType,
                args.Request.ContentType,
                StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail<PhotographUploadDto>(new CatchPhotographNotFoundError());
        }

        var objectKey = CatchPhotographObjectKey.Build(args.CatchId, args.Request.PhotographId);
        var uploadUrl = await _objectStorage.CreateUploadUrlAsync(
            objectKey,
            args.Request.ContentType,
            UploadLifetime,
            cancellationToken);
        return Result.Ok(new PhotographUploadDto(objectKey, uploadUrl.ToString()));
    }

    public async Task<Result> RecordAsync(
        RecordCatchPhotographArgs args,
        CancellationToken cancellationToken)
    {
        var owner = await ResolveCatchOwnerAsync(args.CatchId, cancellationToken);
        if (owner.IsFailed)
        {
            return owner.ToResult();
        }

        var photograph = await LoadOwnedPhotographAsync(
            owner.Value,
            args.CatchId,
            args.PhotographId,
            cancellationToken);
        if (photograph.IsFailed)
        {
            return photograph.ToResult();
        }

        if (!string.Equals(
                photograph.Value.ContentType,
                args.ContentType,
                StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail(new CatchPhotographNotFoundError());
        }

        var expected = CatchPhotographObjectKey.Build(args.CatchId, args.PhotographId);
        return string.Equals(args.ObjectKey, expected, StringComparison.Ordinal)
            ? Result.Ok()
            : Result.Fail(new CatchPhotographObjectKeyMismatchError());
    }

    public async Task<Result> DeleteAsync(
        DeleteCatchPhotographArgs args,
        CancellationToken cancellationToken)
    {
        var owner = await ResolveCatchOwnerAsync(args.CatchId, cancellationToken);
        if (owner.IsFailed)
        {
            return owner.ToResult();
        }

        var photograph = await LoadOwnedPhotographAsync(owner.Value, args.CatchId, args.PhotographId, cancellationToken);
        if (photograph.IsFailed)
        {
            return photograph.ToResult();
        }

        var objectKey = CatchPhotographObjectKey.Build(args.CatchId, args.PhotographId);
        try
        {
            await _objectStorage.DeleteObjectAsync(objectKey, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to delete object storage photograph {PhotographId} for catch {CatchId}.",
                args.PhotographId,
                args.CatchId);
            return Result.Fail(new CatchPhotographStorageDeleteFailedError());
        }

        return await _catchRepository.DeletePhotographAsync(
            new GetCatchPhotographArgs
            {
                UserId = owner.Value,
                CatchId = args.CatchId,
                PhotographId = args.PhotographId
            },
            cancellationToken);
    }

    private async Task<Result<Guid>> ResolveCatchOwnerAsync(Guid catchId, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsResolved)
        {
            return Result.Fail<Guid>(new CurrentUserUnresolvedError());
        }

        var loaded = await _catchRepository.GetByIdAsync(catchId, cancellationToken);
        if (loaded.IsFailed)
        {
            return Result.Fail<Guid>(loaded.Errors);
        }

        if (loaded.Value is null
            || (loaded.Value.AnglerUserId != _currentUser.UserId
                && loaded.Value.RecordedByUserId != _currentUser.UserId))
        {
            return Result.Fail<Guid>(new CatchPhotographNotFoundError());
        }

        return Result.Ok(loaded.Value.UserId);
    }

    private async Task<Result<Domain.Catches.CatchPhotograph>> LoadOwnedPhotographAsync(
        Guid catchOwnerUserId,
        Guid catchId,
        Guid photographId,
        CancellationToken cancellationToken)
    {
        var loaded = await _catchRepository.GetPhotographAsync(
            new GetCatchPhotographArgs
            {
                UserId = catchOwnerUserId,
                CatchId = catchId,
                PhotographId = photographId
            },
            cancellationToken);
        if (loaded.IsFailed)
        {
            return Result.Fail<Domain.Catches.CatchPhotograph>(loaded.Errors);
        }

        return loaded.Value is null
            ? Result.Fail<Domain.Catches.CatchPhotograph>(new CatchPhotographNotFoundError())
            : Result.Ok(loaded.Value);
    }
}
