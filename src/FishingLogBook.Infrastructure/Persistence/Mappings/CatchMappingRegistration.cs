using FishingLogBook.Domain.Catches;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using Mapster;

namespace FishingLogBook.Infrastructure.Persistence.Mappings;

public sealed class CatchMappingRegistration : IRegister
{
    void IRegister.Register(TypeAdapterConfig config)
    {
        config.NewConfig<CatchRepository.CatchPersistenceRow, Catch>()
            .Map(dest => dest.Location, src => ToLocation(
                src.Latitude,
                src.Longitude,
                src.LocationAccuracyMetres,
                src.LocationCapturedOn,
                src.LocationSource,
                src.LocationVisibility,
                src.LocationConsentVersion));
        config.NewConfig<CatchRepository.CatchDetailRow, Catch>()
            .Map(dest => dest.Location, src => ToLocation(
                src.Latitude,
                src.Longitude,
                src.LocationAccuracyMetres,
                src.LocationCapturedOn,
                src.LocationSource,
                src.LocationVisibility,
                src.LocationConsentVersion));
    }

    private static CatchLocation? ToLocation(
        double? latitude,
        double? longitude,
        double? accuracyMetres,
        DateTimeOffset? capturedOn,
        string? source,
        string? visibility,
        string? consentVersion)
    {
        if (latitude is null || longitude is null)
        {
            return null;
        }

        return CatchLocation.TryCreate(
            latitude.Value,
            longitude.Value,
            accuracyMetres,
            capturedOn ?? default,
            source,
            visibility,
            consentVersion);
    }
}
