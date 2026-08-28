using FishingLogBook.Application.Args;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.Trips.Services;

public sealed class TripCatchService : ITripCatchService
{
    private static readonly TimeSpan ClockSkewAllowance = TimeSpan.FromMinutes(5);

    private readonly ITripRepository _tripRepository;
    private readonly ICatchRepository _catchRepository;
    private readonly ICurrentUser _currentUser;

    public TripCatchService(
        ITripRepository tripRepository,
        ICatchRepository catchRepository,
        ICurrentUser currentUser)
    {
        _tripRepository = tripRepository;
        _catchRepository = catchRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<TripCatchAssociationDto>> AssociateAsync(
        AssociateTripCatchesArgs args,
        CancellationToken cancellationToken)
    {
        var trip = await LoadOwnedTripAsync(args.TripId, cancellationToken);
        if (trip.IsFailed)
        {
            return Result.Fail<TripCatchAssociationDto>(trip.Errors);
        }

        var associated = new List<Guid>();
        var rejected = new List<Guid>();
        foreach (var catchId in args.CatchIds.Distinct())
        {
            var outcome = await AssociateOneAsync(trip.Value, catchId, cancellationToken);
            if (outcome.IsFailed)
            {
                return Result.Fail<TripCatchAssociationDto>(outcome.Errors);
            }

            if (outcome.Value)
            {
                associated.Add(catchId);
            }
            else
            {
                rejected.Add(catchId);
            }
        }

        return Result.Ok(new TripCatchAssociationDto(associated, rejected));
    }

    private async Task<Result<bool>> AssociateOneAsync(
        Trip trip,
        Guid catchId,
        CancellationToken cancellationToken)
    {
        var candidate = await _catchRepository.GetByIdAsync(catchId, cancellationToken);
        if (candidate.IsFailed)
        {
            return Result.Fail<bool>(candidate.Errors);
        }

        if (!IsEligible(candidate.Value, trip))
        {
            return Result.Ok(false);
        }

        return await _catchRepository.AssociateTripAsync(
            new PersistCatchTripArgs
            {
                CatchId = catchId,
                UserId = _currentUser.UserId,
                TripId = trip.Id
            },
            cancellationToken);
    }

    private bool IsEligible(Catch? candidate, Trip trip)
    {
        if (candidate is null || candidate.UserId != _currentUser.UserId || candidate.TripId is not null)
        {
            return false;
        }

        if (candidate.CaughtOn < trip.StartedOn)
        {
            return false;
        }

        return candidate.CaughtOn <= (trip.EndedOn ?? DateTimeOffset.UtcNow.Add(ClockSkewAllowance));
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
            return Result.Fail<Trip>(new TripNotFoundError());
        }

        return Result.Ok(trip.Value);
    }
}
