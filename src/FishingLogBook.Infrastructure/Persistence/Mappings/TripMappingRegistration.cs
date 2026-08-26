using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using Mapster;

namespace FishingLogBook.Infrastructure.Persistence.Mappings;

public sealed class TripMappingRegistration : IRegister
{
    void IRegister.Register(TypeAdapterConfig config)
    {
        config.NewConfig<TripRepository.TripPersistenceRow, Trip>()
            .Map(destination => destination.Status, source => ToStatus(source.Status))
            .Map(destination => destination.Location, source => ToLocation(source));
    }

    private static TripStatusEnum ToStatus(string? status)
    {
        return Enum.TryParse<TripStatusEnum>(status, ignoreCase: false, out var parsed)
            ? parsed
            : TripStatusEnum.Completed;
    }

    private static TripLocation? ToLocation(TripRepository.TripPersistenceRow row)
    {
        if (row.Latitude is null || row.Longitude is null)
        {
            return null;
        }

        return TripLocation.TryCreate(
            row.Latitude.Value,
            row.Longitude.Value,
            row.LocationAccuracyMetres,
            row.LocationCapturedOn ?? default,
            row.LocationSource,
            row.LocationVisibility,
            row.LocationConsentVersion);
    }
}
