using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Contracts.Repositories;
using FishingLogBook.Application.Trips.Contracts.Services;
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
    private readonly ITripAccessService _tripAccessService;
    private readonly IMapper _mapper;
    private readonly TimeProvider _timeProvider;

    public TripService(
        ITripRepository tripRepository,
        ITripAccessService tripAccessService,
        IMapper mapper,
        TimeProvider timeProvider)
    {
        _tripRepository = tripRepository;
        _tripAccessService = tripAccessService;
        _mapper = mapper;
        _timeProvider = timeProvider;
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

        var trip = BuildTrip(args, status, location);
        if (!trip.HasCoherentLifecycle())
        {
            return Result.Fail<TripDto>(new TripLifecycleInvalidError());
        }

        var existing = await _tripRepository.GetByIdAsync(trip.Id, cancellationToken);
        if (existing.IsFailed)
        {
            return Result.Fail<TripDto>(existing.Errors);
        }

        var allowed = await CheckTransitionAsync(trip, existing.Value, cancellationToken);
        if (allowed.IsFailed)
        {
            return Result.Fail<TripDto>(allowed.Errors);
        }

        var saved = await _tripRepository.UpsertAsync(trip, cancellationToken);
        if (saved.IsFailed)
        {
            return Result.Fail<TripDto>(saved.Errors);
        }

        return Result.Ok(_mapper.Map<TripDto>(saved.Value));
    }

    public async Task<Result<TripDto>> ActivateAsync(ActivateTripArgs args, CancellationToken cancellationToken)
    {
        var access = await _tripAccessService.RequireContributorAsync(args.TripId, cancellationToken);
        if (access.IsFailed)
        {
            return Result.Fail<TripDto>(access.Errors);
        }

        var trip = access.Value.Trip;
        if (trip.WasActivatedFromPlan)
        {
            return Result.Ok(_mapper.Map<TripDto>(trip));
        }

        if (!trip.IsPlanned)
        {
            return Result.Fail<TripDto>(new TripTransitionInvalidError());
        }

        var activated = await _tripRepository.ActivateAsync(
            trip.ActivatedAt(_timeProvider.GetUtcNow()),
            cancellationToken);
        if (activated.IsFailed)
        {
            return Result.Fail<TripDto>(activated.Errors);
        }

        return Result.Ok(_mapper.Map<TripDto>(activated.Value));
    }

    public async Task<Result<IReadOnlyList<TripSummaryDto>>> GetSummariesAsync(
        GetMyTripsArgs args,
        CancellationToken cancellationToken)
    {
        var loaded = await _tripRepository.GetSummariesForUserAsync(args, cancellationToken);
        if (loaded.IsFailed)
        {
            return Result.Fail<IReadOnlyList<TripSummaryDto>>(loaded.Errors);
        }

        IReadOnlyList<TripSummaryDto> summaries =
        [
            .. loaded.Value.Select(summary => _mapper.Map<TripSummaryDto>(summary) with
            {
                Role = summary.OwnerUserId == args.UserId
                    ? TripParticipantConstants.Owner
                    : TripParticipantConstants.Participant
            })
        ];
        return Result.Ok(summaries);
    }

    private async Task<Result> CheckTransitionAsync(
        Trip trip,
        Trip? existing,
        CancellationToken cancellationToken)
    {
        if (existing is null)
        {
            return Trip.CanBeCreatedAs(trip.Status)
                ? Result.Ok()
                : Result.Fail(new TripTransitionInvalidError());
        }

        var access = await _tripAccessService.RequireOwnerAsync(trip.Id, cancellationToken);
        if (access.IsFailed)
        {
            return access.ToResult();
        }

        return existing.CanBecome(trip.Status)
            ? Result.Ok()
            : Result.Fail(new TripTransitionInvalidError());
    }

    private static Trip BuildTrip(UpsertTripArgs args, TripStatusEnum status, TripLocation? location)
    {
        var hasPlannedLifecycle = TripConstants.HasPlannedLifecycle(args.Trip.Status);
        return new Trip
        {
            Id = args.Trip.Id,
            OwnerUserId = args.UserId,
            Title = TrimToNull(args.Trip.Title),
            PlaceName = TrimToNull(args.Trip.PlaceName),
            Status = status,
            StartedOn = hasPlannedLifecycle ? args.Trip.PlannedStartOn ?? default : args.Trip.StartedOn,
            PlannedStartOn = args.Trip.PlannedStartOn,
            EndedOn = args.Trip.EndedOn,
            Location = location
        };
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
