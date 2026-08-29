using FishingLogBook.Application.Args;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Trips.Services;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripServiceTests;

public class BaseTripServiceTest
{
    protected static readonly Guid CurrentUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    protected readonly ITripRepository MockTripRepository = Substitute.For<ITripRepository>();
    protected readonly ICurrentUser MockCurrentUser = Substitute.For<ICurrentUser>();
    protected readonly TripService Sut;

    protected BaseTripServiceTest()
    {
        MockCurrentUser.IsResolved.Returns(true);
        MockCurrentUser.UserId.Returns(CurrentUserId);
        MockTripRepository.UpsertAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(Persisted(call.ArgAt<Trip>(0))));
        Sut = new TripService(MockTripRepository, TestMapper.Create());
    }

    protected static UpsertTripArgs UpsertArgs(
        Guid? userId = null,
        Guid? tripId = null,
        string status = TripConstants.Active,
        DateTimeOffset? startedOn = null,
        DateTimeOffset? endedOn = null,
        string? title = null,
        string? placeName = null,
        TripLocationDto? location = null)
    {
        return new UpsertTripArgs
        {
            UserId = userId ?? CurrentUserId,
            Trip = new TripDto(
                tripId ?? TripId,
                status,
                startedOn ?? StartedOn,
                endedOn,
                location)
            {
                Title = title,
                PlaceName = placeName
            }
        };
    }

    protected static TripLocationDto PrivateLocation(
        double latitude = 53.4419,
        double longitude = -9.2531,
        string? visibility = null)
    {
        return new TripLocationDto(
            latitude,
            longitude,
            8,
            StartedOn,
            LocationDefaults.DeviceGps,
            visibility ?? LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
    }

    protected static Trip StoredTrip(
        Guid? ownerUserId = null,
        Guid? tripId = null,
        TripStatusEnum status = TripStatusEnum.Active,
        DateTimeOffset? endedOn = null,
        string? title = null,
        string? placeName = null,
        TripLocation? location = null)
    {
        return new Trip
        {
            Id = tripId ?? TripId,
            OwnerUserId = ownerUserId ?? CurrentUserId,
            Title = title,
            PlaceName = placeName,
            Status = status,
            StartedOn = StartedOn,
            EndedOn = endedOn,
            Location = location,
            CreatedOn = StartedOn,
            UpdatedOn = StartedOn
        };
    }

    private static Trip Persisted(Trip trip)
    {
        return new Trip
        {
            Id = trip.Id,
            OwnerUserId = trip.OwnerUserId,
            Title = trip.Title,
            PlaceName = trip.PlaceName,
            Status = trip.Status,
            StartedOn = trip.StartedOn,
            EndedOn = trip.EndedOn,
            Location = trip.Location,
            CreatedOn = StartedOn,
            UpdatedOn = StartedOn
        };
    }
}
