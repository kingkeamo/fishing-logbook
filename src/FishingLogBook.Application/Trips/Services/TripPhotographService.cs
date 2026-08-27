using FishingLogBook.Application.Args;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using MapsterMapper;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Application.Trips.Services;

public sealed class TripPhotographService : ITripPhotographService
{
    private static readonly TimeSpan UploadLifetime = TimeSpan.FromMinutes(15);

    private readonly ITripRepository _tripRepository;
    private readonly ITripPhotographRepository _tripPhotographRepository;
    private readonly IObjectStorage _objectStorage;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;
    private readonly ILogger<TripPhotographService> _logger;

    public TripPhotographService(
        ITripRepository tripRepository,
        ITripPhotographRepository tripPhotographRepository,
        IObjectStorage objectStorage,
        ICurrentUser currentUser,
        IMapper mapper,
        ILogger<TripPhotographService> logger)
    {
        _tripRepository = tripRepository;
        _tripPhotographRepository = tripPhotographRepository;
        _objectStorage = objectStorage;
        _currentUser = currentUser;
        _mapper = mapper;
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
        CreateTripPhotographUploadArgs args,
        CancellationToken cancellationToken)
    {
        var trip = await LoadOwnedTripAsync(args.TripId, cancellationToken);
        if (trip.IsFailed)
        {
            return Result.Fail<PhotographUploadDto>(trip.Errors);
        }

        var objectKey = TripPhotographObjectKey.Build(
            _currentUser.UserId,
            args.TripId,
            args.Request.PhotographId);
        var uploadUrl = await _objectStorage.CreateUploadUrlAsync(
            objectKey,
            args.Request.ContentType,
            UploadLifetime,
            cancellationToken);
        return Result.Ok(new PhotographUploadDto(objectKey, uploadUrl.ToString()));
    }

    public async Task<Result<TripPhotographDto>> RecordAsync(
        RecordTripPhotographArgs args,
        CancellationToken cancellationToken)
    {
        var trip = await LoadOwnedTripAsync(args.TripId, cancellationToken);
        if (trip.IsFailed)
        {
            return Result.Fail<TripPhotographDto>(trip.Errors);
        }

        var expected = TripPhotographObjectKey.Build(
            _currentUser.UserId,
            args.TripId,
            args.PhotographId);
        if (!string.Equals(args.ObjectKey, expected, StringComparison.Ordinal))
        {
            return Result.Fail<TripPhotographDto>(new TripPhotographObjectKeyMismatchError());
        }

        var existing = await _tripPhotographRepository.GetByIdAsync(args.PhotographId, cancellationToken);
        if (existing.IsFailed)
        {
            return Result.Fail<TripPhotographDto>(existing.Errors);
        }

        if (existing.Value is not null && existing.Value.TripId != args.TripId)
        {
            return Result.Fail<TripPhotographDto>(new TripPhotographNotFoundError());
        }

        var saved = await _tripPhotographRepository.UpsertAsync(
            new TripPhotograph
            {
                Id = args.PhotographId,
                TripId = args.TripId,
                ObjectKey = expected,
                ContentType = args.ContentType,
                CapturedOn = args.CapturedOn,
                AddedOn = args.AddedOn
            },
            cancellationToken);
        return saved.IsFailed
            ? Result.Fail<TripPhotographDto>(saved.Errors)
            : Result.Ok(_mapper.Map<TripPhotographDto>(saved.Value));
    }

    public async Task<Result> DeleteAsync(
        DeleteTripPhotographArgs args,
        CancellationToken cancellationToken)
    {
        var trip = await LoadOwnedTripAsync(args.TripId, cancellationToken);
        if (trip.IsFailed)
        {
            return trip.ToResult();
        }

        var photograph = await _tripPhotographRepository.GetByIdAsync(args.PhotographId, cancellationToken);
        if (photograph.IsFailed)
        {
            return photograph.ToResult();
        }

        if (photograph.Value is null || photograph.Value.TripId != args.TripId)
        {
            return Result.Fail(new TripPhotographNotFoundError());
        }

        try
        {
            await _objectStorage.DeleteObjectAsync(photograph.Value.ObjectKey, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to delete stored photograph {PhotographId} for trip {TripId}.",
                args.PhotographId,
                args.TripId);
            return Result.Fail(new TripPhotographNotFoundError());
        }

        return await _tripPhotographRepository.DeleteAsync(args.PhotographId, cancellationToken);
    }

    private async Task<Result<Trip>> LoadOwnedTripAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByIdAsync(tripId, cancellationToken);
        if (trip.IsFailed)
        {
            return Result.Fail<Trip>(trip.Errors);
        }

        if (trip.Value is null || trip.Value.OwnerUserId != _currentUser.UserId)
        {
            return Result.Fail<Trip>(new TripPhotographNotFoundError());
        }

        return Result.Ok(trip.Value);
    }
}
