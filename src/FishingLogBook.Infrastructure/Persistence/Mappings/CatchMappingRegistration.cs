using FishingLogBook.Domain.Catches;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using Mapster;

namespace FishingLogBook.Infrastructure.Persistence.Mappings;

public sealed class CatchMappingRegistration : IRegister
{
    void IRegister.Register(TypeAdapterConfig config)
    {
        config.NewConfig<CatchRepository.CatchPersistenceRow, Catch>()
            .Map(dest => dest.Location, src => ToLocation(src));
    }

    private static CatchLocation? ToLocation(CatchRepository.CatchPersistenceRow row)
    {
        if (row.Latitude is null || row.Longitude is null)
        {
            return null;
        }

        return CatchLocation.TryCreate(
            row.Latitude.Value,
            row.Longitude.Value,
            row.LocationAccuracyMetres,
            row.LocationCapturedOn ?? default,
            row.LocationSource,
            row.LocationVisibility,
            row.LocationConsentVersion);
    }
}
