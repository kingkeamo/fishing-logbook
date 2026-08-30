using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Contracts.Repositories;
using FishingLogBook.Application.Common.Contracts.Services;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.Trips.Services;

public sealed class TripCatchService : ITripCatchService
{
    private static readonly TimeSpan ClockSkewAllowance = TimeSpan.FromMinutes(5);

    private readonly ITripAccessService _tripAccessService;
    private readonly ICatchRepository _catchRepository;
    private readonly ICurrentUser _currentUser;

    public TripCatchService(
        ITripAccessService tripAccessService,
        ICatchRepository catchRepository,
        ICurrentUser currentUser)
    {
        _tripAccessService = tripAccessService;
        _catchRepository = catchRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<TripCatchAssociationDto>> AssociateAsync(
        AssociateTripCatchesArgs args,
        CancellationToken cancellationToken)
    {
        var access = await _tripAccessService.RequireContributorAsync(args.TripId, cancellationToken);
        if (access.IsFailed)
        {
            return Result.Fail<TripCatchAssociationDto>(access.Errors);
        }

        var associated = new List<Guid>();
        var rejected = new List<Guid>();
        foreach (var catchId in args.CatchIds.Distinct())
        {
            var outcome = await AssociateOneAsync(access.Value.Trip, catchId, cancellationToken);
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
}
