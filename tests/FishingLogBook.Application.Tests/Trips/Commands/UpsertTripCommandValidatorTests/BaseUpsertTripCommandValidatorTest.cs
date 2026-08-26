using FishingLogBook.Application.Trips.Commands;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Application.Tests.Trips.Commands.UpsertTripCommandValidatorTests;

public class BaseUpsertTripCommandValidatorTest
{
    protected static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    protected readonly UpsertTripCommandValidator Sut = new();

    protected static UpsertTripCommand Command(
        Guid? userId = null,
        Guid? tripId = null,
        string status = TripConstants.Active,
        DateTimeOffset? startedOn = null,
        DateTimeOffset? endedOn = null,
        string? title = null,
        string? placeName = null,
        TripLocationDto? location = null)
    {
        return new UpsertTripCommand
        {
            UserId = userId ?? UserId,
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

    protected static TripLocationDto Location(
        double latitude = 53.4419,
        double longitude = -9.2531,
        string? visibility = null,
        string? source = null,
        string? consentVersion = null)
    {
        return new TripLocationDto(
            latitude,
            longitude,
            8,
            StartedOn,
            source ?? LocationDefaults.DeviceGps,
            visibility ?? LocationDefaults.Private,
            consentVersion ?? LocationDefaults.ConsentVersion);
    }
}
