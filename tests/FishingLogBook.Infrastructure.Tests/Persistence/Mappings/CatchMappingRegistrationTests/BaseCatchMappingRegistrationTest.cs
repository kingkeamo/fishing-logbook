using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Shared.Dtos;
using MapsterMapper;

namespace FishingLogBook.Infrastructure.Tests.Persistence.Mappings.CatchMappingRegistrationTests;

public abstract class BaseCatchMappingRegistrationTest
{
    protected readonly IMapper Mapper = TestMapper.Create();

    private protected static CatchRepository.CatchRow NewRow(
        double? latitude = null,
        double? longitude = null,
        double? accuracyMetres = 12,
        DateTimeOffset? locationCapturedOn = null,
        string? locationSource = LocationDefaults.DeviceGps,
        string? locationVisibility = LocationDefaults.Private,
        string? locationConsentVersion = LocationDefaults.ConsentVersion)
    {
        return new CatchRepository.CatchRow
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AnglerUserId = Guid.NewGuid(),
            RecordedByUserId = Guid.NewGuid(),
            CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            Latitude = latitude,
            Longitude = longitude,
            LocationAccuracyMetres = accuracyMetres,
            LocationCapturedOn = locationCapturedOn ?? DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            LocationSource = locationSource,
            LocationVisibility = locationVisibility,
            LocationConsentVersion = locationConsentVersion
        };
    }
}
