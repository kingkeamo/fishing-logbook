using FishingLogBook.Domain.Catches;
using Mapster;

namespace FishingLogBook.Infrastructure.Persistence.Mappings;

public sealed class CatchMappingRegistration : IRegister
{
    void IRegister.Register(TypeAdapterConfig config)
    {
        config.NewConfig<CatchRepository.CatchRow, Catch>()
            .MapWith(source => new Catch
            {
                Id = source.Id,
                UserId = source.UserId,
                AnglerUserId = source.AnglerUserId,
                RecordedByUserId = source.RecordedByUserId,
                CaughtOn = source.CaughtOn,
                SpeciesName = source.SpeciesName,
                Weight = source.Weight,
                Length = source.Length,
                Method = source.Method,
                BaitOrLure = source.BaitOrLure,
                Notes = source.Notes,
                Location = ToLocation(source),
                Photographs = source.Photographs
            });
    }

    private static CatchLocation? ToLocation(CatchRepository.CatchRow row)
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
