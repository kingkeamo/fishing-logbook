using FishingLogBook.Application.Args;
using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using MapsterMapper;

namespace FishingLogBook.Application.Trips.Services;

public sealed class TripService : ITripService
{
    private readonly ITripRepository _tripRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public TripService(
        ITripRepository tripRepository,
        ICurrentUser currentUser,
        IMapper mapper)
    {
        _tripRepository = tripRepository;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<Result<TripDto>> UpsertAsync(UpsertTripArgs args, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<TripStatusEnum>(args.Trip.Status, ignoreCase: false, out var status))
        {
            return Result.Fail<TripDto>(new TripLifecycleInvalidError());
        }

        var location = ToLocation(args.Trip.Location);
        if (args.Trip.Location is not null && location is null)
        {
            return Result.Fail<TripDto>(new TripLocationInvalidError());
        }

        var trip = new Trip
        {
            Id = args.Trip.Id,
            OwnerUserId = args.UserId,
            Title = TrimToNull(args.Trip.Title),
            PlaceName = TrimToNull(args.Trip.PlaceName),
            Status = status,
            StartedOn = args.Trip.StartedOn,
            EndedOn = args.Trip.EndedOn,
            Location = location
        };

        if (!trip.HasCoherentLifecycle())
        {
            return Result.Fail<TripDto>(new TripLifecycleInvalidError());
        }

        var saved = await _tripRepository.UpsertAsync(trip, cancellationToken);
        if (saved.IsFailed)
        {
            return Result.Fail<TripDto>(saved.Errors);
        }

        return Result.Ok(_mapper.Map<TripDto>(saved.Value));
    }

    public async Task<Result<TripViewDto>> GetViewAsync(GetTripArgs args, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsResolved)
        {
            return Result.Fail<TripViewDto>(new CurrentUserUnresolvedError());
        }

        var loaded = await _tripRepository.GetByIdAsync(args.TripId, cancellationToken);
        if (loaded.IsFailed)
        {
            return Result.Fail<TripViewDto>(loaded.Errors);
        }

        if (loaded.Value is null || loaded.Value.OwnerUserId != _currentUser.UserId)
        {
            return Result.Fail<TripViewDto>(new TripNotFoundError());
        }

        return Result.Ok(_mapper.Map<TripViewDto>(loaded.Value));
    }

    public async Task<Result<IReadOnlyList<TripViewDto>>> GetMyAsync(
        GetMyTripsArgs args,
        CancellationToken cancellationToken)
    {
        var loaded = await _tripRepository.GetByOwnerUserIdAsync(args.UserId, cancellationToken);
        if (loaded.IsFailed)
        {
            return Result.Fail<IReadOnlyList<TripViewDto>>(loaded.Errors);
        }

        IReadOnlyList<TripViewDto> views = [.. loaded.Value.Select(_mapper.Map<TripViewDto>)];
        return Result.Ok(views);
    }

    private static TripLocation? ToLocation(TripLocationDto? location)
    {
        if (location is null)
        {
            return null;
        }

        return TripLocation.TryCreate(
            location.Latitude,
            location.Longitude,
            location.AccuracyMetres,
            location.CapturedOn,
            location.Source,
            location.Visibility,
            location.ConsentVersion);
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
