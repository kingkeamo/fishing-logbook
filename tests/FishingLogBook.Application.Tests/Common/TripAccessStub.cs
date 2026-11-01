using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Trips;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Common;

public static class TripAccessStub
{
    public static void GivenOwner(this ITripAccessService accessService, Trip trip, Guid userId)
    {
        Configure(accessService, trip, TripAccess.Resolve(trip, userId, participant: null));
    }

    public static void GivenParticipant(this ITripAccessService accessService, Trip trip, Guid userId)
    {
        Configure(
            accessService,
            trip,
            TripAccess.Resolve(trip, userId, ContributingParticipant(trip.Id, userId)));
    }

    public static void GivenNoAccess(this ITripAccessService accessService, Guid tripId)
    {
        accessService.RequireContributorAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripAccess>(new TripNotFoundError()));
        accessService.RequireOpenContributorAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripAccess>(new TripNotFoundError()));
        accessService.RequireOwnerAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripAccess>(new TripNotFoundError()));
        accessService.ResolveAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripAccess>(new TripNotFoundError()));
        accessService.ResolveForAsync(tripId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripAccess>(new TripNotFoundError()));
    }

    public static TripParticipant ContributingParticipant(Guid tripId, Guid userId)
    {
        return new TripParticipant
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            UserId = userId,
            Status = Domain.Enums.TripParticipantStatusEnum.Accepted,
            InvitedByUserId = Guid.NewGuid(),
            InvitedOn = DateTimeOffset.UtcNow.AddDays(-1),
            RespondedOn = DateTimeOffset.UtcNow.AddHours(-1)
        };
    }

    private static Result<TripAccess> OpenContributor(Trip trip, TripAccess access)
    {
        if (!access.CanContribute)
        {
            return Result.Fail<TripAccess>(new TripNotFoundError());
        }

        return trip.AcceptsContributions
            ? Result.Ok(access)
            : Result.Fail<TripAccess>(new TripNotOpenError());
    }

    private static void Configure(ITripAccessService accessService, Trip trip, TripAccess access)
    {
        accessService.ResolveAsync(trip.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(access));
        accessService.ResolveForAsync(trip.Id, access.UserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(access));
        accessService.RequireContributorAsync(trip.Id, Arg.Any<CancellationToken>())
            .Returns(access.CanContribute
                ? Result.Ok(access)
                : Result.Fail<TripAccess>(new TripNotFoundError()));
        accessService.RequireOpenContributorAsync(trip.Id, Arg.Any<CancellationToken>())
            .Returns(OpenContributor(trip, access));
        accessService.RequireOwnerAsync(trip.Id, Arg.Any<CancellationToken>())
            .Returns(access.CanManageTrip
                ? Result.Ok(access)
                : Result.Fail<TripAccess>(access.CanView
                    ? new TripOwnerActionRequiredError()
                    : new TripNotFoundError()));
    }
}
