using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Commands;
using FishingLogBook.Application.Catches.Queries;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Shared.Dtos;
using Mapster;

namespace FishingLogBook.Application.Common.Mappings;

public sealed class CatchMappingRegistration : IRegister
{
    void IRegister.Register(TypeAdapterConfig config)
    {
        config.NewConfig<CatchDto, CatchDto>()
            .MapWith(source => source);
        config.NewConfig<RecordCatchPhotographCommand, RecordCatchPhotographArgs>()
            .Map(destination => destination.PhotographId, source => source.Photograph.PhotographId)
            .Map(destination => destination.ObjectKey, source => source.Photograph.ObjectKey)
            .Map(destination => destination.ContentType, source => source.Photograph.ContentType);
        config.NewConfig<Catch, CatchDto>()
            .MapWith(source => new CatchDto(
                source.Id,
                source.CaughtOn,
                source.Photographs
                    .Select(photograph => new CatchPhotographDto(
                        photograph.Id,
                        photograph.CatchId,
                        photograph.ContentType))
                    .ToList(),
                source.Location == null
                    ? null
                    : new CatchLocationDto(
                        source.Location.Latitude,
                        source.Location.Longitude,
                        source.Location.AccuracyMetres,
                        source.Location.CapturedOn,
                        source.Location.Source,
                        source.Location.Visibility,
                        source.Location.ConsentVersion))
            {
                UserId = source.UserId,
                AnglerUserId = source.AnglerUserId,
                RecordedByUserId = source.RecordedByUserId,
                SpeciesName = source.SpeciesName,
                Weight = source.Weight,
                Length = source.Length,
                Method = source.Method,
                BaitOrLure = source.BaitOrLure,
                Notes = source.Notes
            });
    }
}
