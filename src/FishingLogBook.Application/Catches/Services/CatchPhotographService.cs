using FishingLogBook.Application.Args;
using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.Catches.Services;

public sealed class CatchPhotographService : ICatchPhotographService
{
    private static readonly TimeSpan UploadLifetime = TimeSpan.FromMinutes(15);

    private readonly ICatchRepository _catchRepository;
    private readonly IObjectStorage _objectStorage;
    private readonly ICurrentUser _currentUser;

    public CatchPhotographService(
        ICatchRepository catchRepository,
        IObjectStorage objectStorage,
        ICurrentUser currentUser)
    {
        _catchRepository = catchRepository;
        _objectStorage = objectStorage;
        _currentUser = currentUser;
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
        var photograph = await LoadOwnedPhotographAsync(
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

        var objectKey = CatchPhotographObjectKey.Build(_currentUser.UserId, args.CatchId, args.Request.PhotographId);
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
        var photograph = await LoadOwnedPhotographAsync(
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

        var expected = CatchPhotographObjectKey.Build(_currentUser.UserId, args.CatchId, args.PhotographId);
        return string.Equals(args.ObjectKey, expected, StringComparison.Ordinal)
            ? Result.Ok()
            : Result.Fail(new CatchPhotographObjectKeyMismatchError());
    }

    private async Task<Result<Domain.Catches.CatchPhotograph>> LoadOwnedPhotographAsync(
        Guid catchId,
        Guid photographId,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsResolved)
        {
            return Result.Fail<Domain.Catches.CatchPhotograph>(new CurrentUserUnresolvedError());
        }

        var loaded = await _catchRepository.GetPhotographAsync(
            new GetCatchPhotographArgs
            {
                UserId = _currentUser.UserId,
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
