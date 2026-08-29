using FishingLogBook.Application.Args;
using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Trips;
using FluentResults;

namespace FishingLogBook.Application.Trips.Services;

public sealed class TripAccessService : ITripAccessService
{
    private readonly ITripRepository _tripRepository;
    private readonly ITripParticipantRepository _tripParticipantRepository;
    private readonly ICurrentUser _currentUser;

    public TripAccessService(
        ITripRepository tripRepository,
        ITripParticipantRepository tripParticipantRepository,
        ICurrentUser currentUser)
    {
        _tripRepository = tripRepository;
        _tripParticipantRepository = tripParticipantRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<TripAccess>> ResolveAsync(Guid tripId, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsResolved)
        {
            return Result.Fail<TripAccess>(new CurrentUserUnresolvedError());
        }

        return await ResolveForAsync(tripId, _currentUser.UserId, cancellationToken);
    }

    public async Task<Result<TripAccess>> ResolveForAsync(
        Guid tripId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByIdAsync(tripId, cancellationToken);
        if (trip.IsFailed)
        {
            return Result.Fail<TripAccess>(trip.Errors);
        }

        if (trip.Value is null)
        {
            return Result.Fail<TripAccess>(new TripNotFoundError());
        }

        if (trip.Value.OwnerUserId == userId)
        {
            return Result.Ok(TripAccess.Resolve(trip.Value, userId, participant: null));
        }

        var participant = await _tripParticipantRepository.FindAsync(
            new FindTripParticipantArgs { TripId = tripId, UserId = userId },
            cancellationToken);
        if (participant.IsFailed)
        {
            return Result.Fail<TripAccess>(participant.Errors);
        }

        return Result.Ok(TripAccess.Resolve(trip.Value, userId, participant.Value));
    }

    public async Task<Result<TripAccess>> RequireContributorAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var access = await ResolveAsync(tripId, cancellationToken);
        if (access.IsFailed)
        {
            return access;
        }

        // A non-participant must not learn that the trip exists.
        return access.Value.CanContribute
            ? access
            : Result.Fail<TripAccess>(new TripNotFoundError());
    }

    public async Task<Result<TripAccess>> RequireOwnerAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var access = await ResolveAsync(tripId, cancellationToken);
        if (access.IsFailed)
        {
            return access;
        }

        if (access.Value.CanManageTrip)
        {
            return access;
        }

        // Fail closed: only a viewer who already sees the trip learns it is owner-only.
        return Result.Fail<TripAccess>(access.Value.CanView
            ? new TripOwnerActionRequiredError()
            : new TripNotFoundError());
    }
}
