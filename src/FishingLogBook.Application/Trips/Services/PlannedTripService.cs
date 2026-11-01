using FishingLogBook.Application.Args;
using FishingLogBook.Application.Profiles.Contracts.Services;
using FishingLogBook.Application.Trips.Contracts.Repositories;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.Trips.Services;

public sealed class PlannedTripService : IPlannedTripService
{
    private readonly ITripRepository _tripRepository;
    private readonly ITripParticipantRepository _tripParticipantRepository;
    private readonly IAnglerLookupService _anglerLookupService;
    private readonly TimeProvider _timeProvider;

    public PlannedTripService(
        ITripRepository tripRepository,
        ITripParticipantRepository tripParticipantRepository,
        IAnglerLookupService anglerLookupService,
        TimeProvider timeProvider)
    {
        _tripRepository = tripRepository;
        _tripParticipantRepository = tripParticipantRepository;
        _anglerLookupService = anglerLookupService;
        _timeProvider = timeProvider;
    }

    public async Task<Result<IReadOnlyList<PlannedTripOptionDto>>> GetEligibleAsync(
        GetPlannedTripsArgs args,
        CancellationToken cancellationToken)
    {
        var windowDays = TripConstants.NormalisePlannedWindowDays(args.WindowDays);
        var now = _timeProvider.GetUtcNow();
        var window = TimeSpan.FromDays(windowDays);
        var found = await _tripRepository.FindPlannedAsync(
            new FindPlannedTripsArgs
            {
                UserId = args.UserId,
                From = now - window,
                To = now + window
            },
            cancellationToken);
        if (found.IsFailed)
        {
            return Result.Fail<IReadOnlyList<PlannedTripOptionDto>>(found.Errors);
        }

        var eligible = found.Value
            .Where(trip => trip.IsPlanned
                && trip.PlannedStartOn is { } plannedStartOn
                && TripConstants.IsWithinPlannedWindow(plannedStartOn, now, windowDays))
            .OrderBy(trip => trip.PlannedStartOn)
            .ThenBy(trip => trip.Id)
            .ToArray();
        if (eligible.Length == 0)
        {
            return Result.Ok<IReadOnlyList<PlannedTripOptionDto>>([]);
        }

        return await DescribeAsync(args.UserId, eligible, cancellationToken);
    }

    private async Task<Result<IReadOnlyList<PlannedTripOptionDto>>> DescribeAsync(
        Guid userId,
        IReadOnlyList<Trip> trips,
        CancellationToken cancellationToken)
    {
        var participantsByTrip = new Dictionary<Guid, IReadOnlyList<TripParticipant>>();
        foreach (var trip in trips)
        {
            var participants = await _tripParticipantRepository.GetByTripIdAsync(trip.Id, cancellationToken);
            if (participants.IsFailed)
            {
                return Result.Fail<IReadOnlyList<PlannedTripOptionDto>>(participants.Errors);
            }

            participantsByTrip[trip.Id] =
            [
                .. participants.Value
                    .Where(participant => participant.IsPending || participant.IsContributing)
                    .OrderBy(participant => participant.InvitedOn)
                    .ThenBy(participant => participant.UserId)
            ];
        }

        var described = await _anglerLookupService.DescribeAsync(
            [
                .. trips.Select(trip => trip.OwnerUserId),
                .. participantsByTrip.Values.SelectMany(participants => participants.Select(participant => participant.UserId))
            ],
            cancellationToken);
        if (described.IsFailed)
        {
            return Result.Fail<IReadOnlyList<PlannedTripOptionDto>>(described.Errors);
        }

        IReadOnlyList<PlannedTripOptionDto> options =
        [
            .. trips.Select(trip => ToOption(userId, trip, participantsByTrip[trip.Id], described.Value))
        ];
        return Result.Ok(options);
    }

    private static PlannedTripOptionDto ToOption(
        Guid userId,
        Trip trip,
        IReadOnlyList<TripParticipant> participants,
        IReadOnlyDictionary<Guid, AnglerSummaryDto> described)
    {
        return new PlannedTripOptionDto(
            trip.Id,
            trip.PlannedStartOn!.Value,
            trip.OwnerUserId,
            described.GetValueOrDefault(trip.OwnerUserId)?.DisplayName)
        {
            Title = trip.Title,
            PlaceName = trip.PlaceName,
            Role = trip.OwnerUserId == userId
                ? TripParticipantConstants.Owner
                : TripParticipantConstants.Participant,
            Participants =
            [
                .. participants.Select(participant => new PlannedTripParticipantDto(
                    participant.UserId,
                    described.GetValueOrDefault(participant.UserId)?.DisplayName,
                    participant.Status.ToString()))
            ]
        };
    }
}
