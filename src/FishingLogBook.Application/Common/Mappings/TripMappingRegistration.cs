using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Dtos;
using Mapster;

namespace FishingLogBook.Application.Common.Mappings;

public sealed class TripMappingRegistration : IRegister
{
    void IRegister.Register(TypeAdapterConfig config)
    {
        config.NewConfig<TripDto, TripDto>()
            .MapWith(source => source);

        config.NewConfig<Trip, TripDto>()
            .MapWith(source => new TripDto(
                source.Id,
                source.Status.ToString(),
                source.StartedOn,
                source.EndedOn,
                source.Location == null
                    ? null
                    : new TripLocationDto(
                        source.Location.Latitude,
                        source.Location.Longitude,
                        source.Location.AccuracyMetres,
                        source.Location.CapturedOn,
                        source.Location.Source,
                        source.Location.Visibility,
                        source.Location.ConsentVersion))
            {
                OwnerUserId = source.OwnerUserId,
                Title = source.Title,
                PlaceName = source.PlaceName
            });

        config.NewConfig<Trip, TripViewDto>()
            .MapWith(source => new TripViewDto(
                source.Id,
                source.OwnerUserId,
                source.Status.ToString(),
                source.StartedOn,
                source.EndedOn,
                source.Location == null
                    ? null
                    : new TripLocationDto(
                        source.Location.Latitude,
                        source.Location.Longitude,
                        source.Location.AccuracyMetres,
                        source.Location.CapturedOn,
                        source.Location.Source,
                        source.Location.Visibility,
                        source.Location.ConsentVersion))
            {
                Title = source.Title,
                PlaceName = source.PlaceName,
                CreatedOn = source.CreatedOn,
                UpdatedOn = source.UpdatedOn
            });
    }
}
