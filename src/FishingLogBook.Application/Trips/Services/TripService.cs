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

    public TripService(
        ITripRepository tripRepository,
        ITripAccessService tripAccessService,
        IMapper mapper)
    {
        _tripRepository = tripRepository;
        _tripAccessService = tripAccessService;
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

        var existing = await _tripRepository.GetByIdAsync(trip.Id, cancellationToken);
        if (existing.IsFailed)
        {
            return Result.Fail<TripDto>(existing.Errors);
        }

        if (existing.Value is not null)
        {
            var access = await _tripAccessService.RequireOwnerAsync(trip.Id, cancellationToken);
            if (access.IsFailed)
            {
                return Result.Fail<TripDto>(access.Errors);
            }
        }

        var saved = await _tripRepository.UpsertAsync(trip, cancellationToken);
        if (saved.IsFailed)
        {
            return Result.Fail<TripDto>(saved.Errors);
        }

        return Result.Ok(_mapper.Map<TripDto>(saved.Value));
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
