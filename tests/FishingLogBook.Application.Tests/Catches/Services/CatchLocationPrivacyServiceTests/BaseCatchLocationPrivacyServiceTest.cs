using AwesomeAssertions;
using FishingLogBook.Application.Catches.Services;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Application.Tests.Catches.Services.CatchLocationPrivacyServiceTests;

public class BaseCatchLocationPrivacyServiceTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected static readonly Guid ViewerUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected readonly CatchLocationPrivacyService Sut = new();

    protected static Catch LocatedCatch(
        Guid ownerUserId,
        string visibility = LocationDefaults.Private,
        double latitude = 53.2707,
        double longitude = -9.0568)
    {
        return new Catch
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            UserId = ownerUserId,
            CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            Location = CatchLocation.TryCreate(
                latitude,
                longitude,
                5,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                LocationDefaults.DeviceGps,
                visibility,
                LocationDefaults.ConsentVersion)
        };
    }
}
